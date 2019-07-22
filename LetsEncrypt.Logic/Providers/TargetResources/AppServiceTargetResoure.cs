using LetsEncrypt.Logic.Authentication;
using LetsEncrypt.Logic.Extensions;
using LetsEncrypt.Logic.Providers.CertificateStores;
using Microsoft.Azure.Services.AppAuthentication;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace LetsEncrypt.Logic.Providers.TargetResources
{
    public class AppServiceTargetResoure : ITargetResource
    {
        private readonly string _resourceGroupName;
        private readonly IAzureHelper _azureHelper;

        public AppServiceTargetResoure(IAzureHelper azureHelper, string resourceGroupName, string name)
        {
            _azureHelper = azureHelper ?? throw new ArgumentNullException(nameof(azureHelper));
            _resourceGroupName = resourceGroupName ?? throw new ArgumentNullException(nameof(resourceGroupName));
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        public string Name { get; }
        public string Type => "App Service";

        public async Task UpdateAsync(ICertificate cert, CancellationToken cancellationToken)
        {
            if (cert.Store.Type != "keyVault")
                throw new NotSupportedException("App Service can only use certificates from store keyVault. Found: " + cert.Store.Type);

            // based on: https://azure.github.io/AppService/2016/05/24/Deploying-Azure-Web-App-Certificate-through-Key-Vault.html

            // fluent api would be nicer to use (mgmt api preview package already offers new endpoints, but fluent api does not)
            // but problematic: neither api supports fallback from MSI to local user (both requiring MSI_ENDPOINT env variable)
            // see https://github.com/Azure/azure-libraries-for-net/issues/585

            var httpClient = await GetAuthenticatedClientAsync(cancellationToken);

            // actually find the bindings which use the cert
            // e.g. cert input "www.example.com, example.com" will have two seperate bindings for the domains
            var response = await GetAppServicePropertiesAsync(httpClient, cancellationToken);

            // user may also provide X hostnames in cert, but then map them to Y different webapps
            // get hostnames from webapp and only return the matching set
            var hostnames = cert.HostNames
                .Where(h => response.Hostnames.Contains(h, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (!hostnames.Any())
                throw new InvalidOperationException($"Web app {Name} has no matching domain assigned to it from hostname set: {string.Join(", ", cert.HostNames)}");

            // cert name should be unique in resourcegroup yet it must be possible to upload multiple certificates for the same domain to allow for cert rotation -> append thumbprint
            var certName = $"{hostnames.First()}-{cert.Thumbprint}";

            // upload certificate into app service plan resource group
            await UploadCertificateAsync(response, httpClient, cert, certName, cancellationToken);

            await AssignDomainBindingsAsync(hostnames, cert, response.Location, httpClient, cancellationToken);
        }

        private async Task AssignDomainBindingsAsync(string[] hostnames, ICertificate cert, string location, HttpClient httpClient, CancellationToken cancellationToken)
        {
            var errors = new List<Exception>();
            foreach (var domain in hostnames)
            {
                // bind cert to domain
                var certificateBindUrl = "https://management.azure.com" +
                        $"/subscriptions/{_azureHelper.GetSubscriptionId()}/" +
                        $"resourceGroups/{_resourceGroupName}/" +
                        $"providers/Microsoft.Web/sites/{Name}/hostNameBindings/{domain}?api-version=2018-11-01";
                var content = new StringContent(JsonConvert.SerializeObject(new
                {
                    location,
                    properties = new
                    {
                        sslState = "SniEnabled",
                        thumbprint = cert.Thumbprint
                    }
                }), Encoding.UTF8, "application/json");
                var response = await httpClient.PutAsync(certificateBindUrl, content, cancellationToken);
                try
                {
                    await response.EnsureSuccessAsync($"Failed to assign certificate to domain {domain} of webapp {Name}.");
                }
                catch (Exception e)
                {
                    errors.Add(e);
                }
            }
            if (errors.Any())
                throw new AggregateException($"Domain bindings failed for certificate {cert.Name} on web app {Name}", errors);
        }

        private async Task UploadCertificateAsync(
            AppServiceProperties prop,
            HttpClient httpClient,
            ICertificate cert,
            string uploadCertName,
            CancellationToken cancellationToken)
        {
            // documentation used to be adamant about keeping cert next to app service plan, but seems its now also possible to keep it next to web app itself
            // (cert can never be moved once it is bound); keep next to app service plan for now
            var appServiceResourceGroup = ParseResourceGroupFromResourceId(prop.ServerFarmId);

            var certificateUploadUrl = "https://management.azure.com" +
                    $"/subscriptions/{_azureHelper.GetSubscriptionId()}/" +
                    $"resourceGroups/{appServiceResourceGroup}/" +
                    $"providers/Microsoft.Web/certificates/{uploadCertName}?api-version=2018-11-01";
            var content = new StringContent(JsonConvert.SerializeObject(new
            {
                prop.Location,
                properties = new
                {
                    keyVaultId = cert.Store.ResourceId,
                    keyVaultSecretName = cert.Name,
                    prop.ServerFarmId,
                }
            }), Encoding.UTF8, "application/json");

            var response = await httpClient.PutAsync(certificateUploadUrl, content, cancellationToken);
            await response.EnsureSuccessAsync($"Failed to upload certificate {uploadCertName} to resource group {_resourceGroupName}.");
        }

        private async Task<AppServiceProperties> GetAppServicePropertiesAsync(HttpClient httpClient, CancellationToken cancellationToken)
        {
            var appServiceUrl = "https://management.azure.com" +
                    $"/subscriptions/{_azureHelper.GetSubscriptionId()}/" +
                    $"resourceGroups/{_resourceGroupName}/" +
                    $"providers/Microsoft.Web/sites/{Name}?api-version=2018-11-01";

            var response = await httpClient.GetAsync(appServiceUrl, cancellationToken);
            await response.EnsureSuccessAsync($"Failed to query website {Name} in resource group {_resourceGroupName}.");
            var appServiceResponse = JsonConvert.DeserializeAnonymousType(await response.Content.ReadAsStringAsync(), new
            {
                location = "",
                properties = new
                {
                    serverFarmId = "",
                    enabledHostNames = new[]
                    {
                        // contains scm, azurewebsites and custom hostnames all in one
                        ""
                    }
                }
            });

            return new AppServiceProperties
            {
                Hostnames = appServiceResponse.properties.enabledHostNames,
                Location = appServiceResponse.location,
                ServerFarmId = appServiceResponse.properties.serverFarmId
            };
        }

        private async Task<HttpClient> GetAuthenticatedClientAsync(CancellationToken cancellationToken)
        {
            var tokenProvider = new AzureServiceTokenProvider();
            var tenantId = await _azureHelper.GetTenantIdAsync(cancellationToken);
            // only allow connections to management API with this provider
            // we only use it to update CDN after cert deployment
            const string msiTokenprovider = "https://management.azure.com/";
            var cred = new MsiTokenProvider(tokenProvider, tenantId,
                req => req.RequestUri.ToString().StartsWith(msiTokenprovider)
                    ? msiTokenprovider
                    : throw new InvalidOperationException($"Token issuer was asked for a token for '{req.RequestUri}' but is only allowed to issue tokens for '{msiTokenprovider}'"));

            return new HttpClient(cred);
        }

        private static string ParseResourceGroupFromResourceId(string serverFarmId)
        {
            var regex = new Regex(@"^\/subscriptions\/[\w-]+/resourceGroups\/([\w-]+)\/");

            var match = regex.Match(serverFarmId);
            if (!match.Success)
                throw new NotSupportedException($"Unable to parse resourcegroup from resourceId: {serverFarmId}");

            return match.Groups[1].Value;
        }

        private class AppServiceProperties
        {
            public string Location { get; set; }

            public string ServerFarmId { get; set; }

            public string[] Hostnames { get; set; }
        }
    }
}
