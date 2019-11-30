using LetsEncrypt.Logic.Azure.Response;
using LetsEncrypt.Logic.Extensions;
using LetsEncrypt.Logic.Providers.CertificateStores;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LetsEncrypt.Logic.Azure
{
    /// <summary>
    /// Minimal wrapper for app service REST calls.
    /// based on: https://azure.github.io/AppService/2016/05/24/Deploying-Azure-Web-App-Certificate-through-Key-Vault.html
    /// fluent api would be nicer to use (mgmt api preview package already offers new endpoints, but fluent api does not)
    /// but problematic: neither api supports fallback from MSI to local user (both requiring MSI_ENDPOINT env variable) -> can't test locally
    /// see https://github.com/Azure/azure-libraries-for-net/issues/585
    /// Where possible this should be replaced by fluent mgmt api package but see individual TargetResources for reasons why this is currently not possible
    /// </summary>
    public class AzureAppServiceClient : IAzureAppServiceClient
    {
        private readonly IAzureHelper _azureHelper;
        private HttpClient _httpClient;
        private readonly ILogger _logger;

        public AzureAppServiceClient(
            IAzureHelper azureHelper,
            ILogger logger)
        {
            _azureHelper = azureHelper;
            _logger = logger;
        }

        public async Task<AppServiceResponse> GetAppServicePropertiesAsync(string resourceGroupName, string name, CancellationToken cancellationToken)
        {
            var appServiceUrl = "https://management.azure.com" +
                    $"/subscriptions/{_azureHelper.GetSubscriptionId()}/" +
                    $"resourceGroups/{resourceGroupName}/" +
                    $"providers/Microsoft.Web/sites/{name}?api-version=2018-11-01";

            var httpClient = await _azureHelper.GetAuthenticatedARMClientAsync(cancellationToken);
            var response = await httpClient.GetAsync(appServiceUrl, cancellationToken);
            await response.EnsureSuccessAsync($"Failed to query website {name} in resource group {resourceGroupName}.");
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

            return new AppServiceResponse
            {
                Hostnames = appServiceResponse.properties.enabledHostNames,
                Location = appServiceResponse.location,
                ServerFarmId = appServiceResponse.properties.serverFarmId
            };
        }

        public async Task AssignDomainBindingsAsync(string resourceGroupName, string name, string[] hostnames, ICertificate cert, string location, CancellationToken cancellationToken)
        {
            var errors = new List<Exception>();
            foreach (var domain in hostnames)
            {
                // bind cert to domain
                var response = await BindAppServiceCertificateSniAsync(resourceGroupName, name, domain, location, cert.Thumbprint, cancellationToken);
                try
                {
                    await response.EnsureSuccessAsync($"Failed to assign certificate to domain {domain} of webapp {name}.");
                }
                catch (Exception e)
                {
                    errors.Add(e);
                }
            }
            if (errors.Any())
                throw new AggregateException($"Domain bindings failed for certificate {cert.Name} on web app {name}", errors);
        }

        public async Task UploadCertificateAsync(
            AppServiceResponse prop,
            ICertificate cert,
            string uploadCertName,
            string targetResourceGroup,
            CancellationToken cancellationToken)
        {
            var certificateUploadUrl = "https://management.azure.com" +
                    $"/subscriptions/{_azureHelper.GetSubscriptionId()}/" +
                    $"resourceGroups/{targetResourceGroup}/" +
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

            var httpClient = await _azureHelper.GetAuthenticatedARMClientAsync(cancellationToken);
            var response = await httpClient.PutAsync(certificateUploadUrl, content, cancellationToken);
            await response.EnsureSuccessAsync($"Failed to upload certificate {uploadCertName} to resource group {targetResourceGroup}.");
        }

        public async Task DeleteCertificateAsync(
            string certName,
            string resourceGroupName,
            CancellationToken cancellationToken)
        {
            var deleteCertificatesInResourceGroupUrl = "https://management.azure.com" +
                       $"/subscriptions/{_azureHelper.GetSubscriptionId()}/" +
                       $"resourceGroups/{resourceGroupName}/" +
                       $"providers/Microsoft.Web/certificates/{certName}?api-version=2016-03-01";

            var httpClient = await _azureHelper.GetAuthenticatedARMClientAsync(cancellationToken);
            try
            {
                var response = await httpClient.DeleteAsync(deleteCertificatesInResourceGroupUrl, cancellationToken);
                await response.EnsureSuccessAsync($"Failed to delete certificate {certName} in resource group {resourceGroupName}.");
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Failed to delete certificate {certName} in resourcegroup {resourceGroupName}.");
            }
        }

        private async Task<HttpResponseMessage> BindAppServiceCertificateSniAsync(string resourceGroupName, string name, string domain, string location, string thumbprint, CancellationToken cancellationToken)
        {
            var certificateBindUrl = "https://management.azure.com" +
                    $"/subscriptions/{_azureHelper.GetSubscriptionId()}/" +
                    $"resourceGroups/{resourceGroupName}/" +
                    $"providers/Microsoft.Web/sites/{name}/hostNameBindings/{domain}?api-version=2018-11-01";
            var content = new StringContent(JsonConvert.SerializeObject(new
            {
                location,
                properties = new
                {
                    sslState = "SniEnabled",
                    thumbprint = thumbprint
                }
            }), Encoding.UTF8, "application/json");

            var httpClient = await _azureHelper.GetAuthenticatedARMClientAsync(cancellationToken);
            return await httpClient.PutAsync(certificateBindUrl, content, cancellationToken);
        }

        public async Task<CertificateResponse[]> ListCertificatesAsync(string resourceGroupName, CancellationToken cancellationToken)
        {
            // each cert has a unique name in the RG, delete all old certificate resources, except for the current one
            var certificatesInResourceGroupUrl = "https://management.azure.com" +
                    $"/subscriptions/{_azureHelper.GetSubscriptionId()}/" +
                    $"resourceGroups/{resourceGroupName}/" +
                    "providers/Microsoft.Web/certificates?api-version=2016-03-01";

            var httpClient = await _azureHelper.GetAuthenticatedARMClientAsync(cancellationToken);
            var response = await httpClient.GetAsync(certificatesInResourceGroupUrl, cancellationToken);
            await response.EnsureSuccessAsync($"Failed to query certificates in resource group {resourceGroupName}.");
            var certificates = JsonConvert.DeserializeAnonymousType(await response.Content.ReadAsStringAsync(), new
            {
                value = new[]
                {
                    new
                    {
                        name = "",
                        properties = new
                        {
                            hostNames = new string[0],
                            thumbprint = ""
                        }
                    }
                }
            });

            return certificates.value
                .Select(x => new CertificateResponse
                {
                    Name = x.name,
                    HostNames = x.properties.hostNames,
                    Thumbprint = x.properties.thumbprint
                })
                .ToArray();
        }
    }
}
