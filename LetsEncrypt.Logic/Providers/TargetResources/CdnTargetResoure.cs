using LetsEncrypt.Logic.Authentication;
using LetsEncrypt.Logic.Extensions;
using LetsEncrypt.Logic.Providers.CertificateStores;
using Microsoft.Azure.Services.AppAuthentication;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LetsEncrypt.Logic.Providers.TargetResources
{
    public class CdnTargetResoure : ITargetResource
    {
        private readonly string _resourceGroupName;
        private readonly string[] _endpoints;
        private readonly IAzureHelper _azureHelper;

        public CdnTargetResoure(IAzureHelper azureHelper, string resourceGroupName, string name, string[] endpoints)
        {
            _azureHelper = azureHelper ?? throw new ArgumentNullException(nameof(azureHelper));
            _resourceGroupName = resourceGroupName ?? throw new ArgumentNullException(nameof(resourceGroupName));
            Name = name ?? throw new ArgumentNullException(nameof(name));
            _endpoints = endpoints ?? throw new ArgumentNullException(nameof(endpoints));
        }

        public string Name { get; }

        public string Type => "CDN";

        public async Task UpdateAsync(ICertificate cert, CancellationToken cancellationToken)
        {
            if (cert.Store.Type != "keyVault")
                throw new NotSupportedException("Azure CDN can only use certificates from store keyVault. Found: " + cert.Store.Type);

            // testing sleep after cert is uploaded to possibly working around a bug..
            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);

            // use REST directly because nuget packages don't contain the required endpoint to update CDN yet
            // fluent api would be nicer to use (mgmt api preview package already offers new endpoints, but fluent api does not)
            // but problematic: neither api supports fallback from MSI to local user (both requiring MSI_ENDPOINT env variable)
            // see https://github.com/Azure/azure-libraries-for-net/issues/585

            var tokenProvider = new AzureServiceTokenProvider();
            var tenantId = await _azureHelper.GetTenantIdAsync(cancellationToken);
            // only allow connections to management API with this provider
            // we only use it to update CDN after cert deployment
            const string msiTokenprovider = "https://management.azure.com/";
            var cred = new MsiTokenProvider(tokenProvider, tenantId,
                req => req.RequestUri.ToString().StartsWith(msiTokenprovider)
                    ? msiTokenprovider
                    : throw new InvalidOperationException($"Token issuer was asked for a token for '{req.RequestUri}' but is only allowed to issue tokens for '{msiTokenprovider}'"));

            var httpClient = new HttpClient(cred);

            // actually find the endpoints who have matching certs
            // e.g. cert input "www.example.com, example.com" will have two seperate endpoints for the domains
            // user could also possibly misconfigure (include an endpoint for which we are not issuing a cert)
            // => don't want to break that either..

            var listEndpointUrl = "https://management.azure.com" +
                    $"/subscriptions/{_azureHelper.GetSubscriptionId()}/" +
                    $"resourceGroups/{_resourceGroupName}/" +
                    $"providers/Microsoft.Cdn/profiles/{Name}/endpoints?api-version=2019-04-15";

            var response = await httpClient.GetAsync(listEndpointUrl, cancellationToken);
            response.EnsureSuccessStatusCode();
            var responseContent = await response.Content.ReadAsStringAsync();
            var endpoints = JsonConvert.DeserializeAnonymousType(responseContent, new
            {
                value = new[]
                {
                    new
                    {
                        // endpoint name
                        name = "",
                        properties = new
                        {
                            customDomains = new[]
                            {
                                new
                                {
                                    // will be normalized, i.e. example-com
                                    name = "",
                                    properties = new
                                    {
                                        // will be example.com
                                        hostName = ""
                                    }
                                }
                            }
                        }
                    }
                }
            });
            var matchingEndpoints = endpoints.value
                .Where(endpoint => _endpoints.Contains(endpoint.name, StringComparison.OrdinalIgnoreCase) &&
                            endpoint.properties.customDomains.Any(domain =>
                                cert.HostNames.Contains(domain.properties.hostName, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            // use REST api, management SDK doesn't have new endpoints yet (fluent SDK not at all, regular mgmt SDK only in preview release)
            // https://stackoverflow.com/a/56147987
            // update all endpoints in parallel
            var results = await Task.WhenAll(matchingEndpoints
                .SelectMany(e => e.properties.customDomains, (endpoint, domain) =>
            {
                // https://github.com/Azure/azure-rest-api-specs/blob/master/specification/cdn/resource-manager/Microsoft.Cdn/stable/2019-04-15/examples/CustomDomains_EnableCustomHttpsUsingBYOC.json
                // switched to older API as per https://stackoverflow.com/a/56147987 since 2019-04-15 sometimes just removes the certificate without updating it..
                var url = "https://management.azure.com" +
                    $"/subscriptions/{_azureHelper.GetSubscriptionId()}/" +
                    $"resourceGroups/{_resourceGroupName}/" +
                    $"providers/Microsoft.Cdn/profiles/{Name}/" +
                    $"endpoints/{endpoint.name}/customDomains/" +
                    $"{domain.name}/enableCustomHttps?api-version=2018-04-02";

                var settings = new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented,
                    ContractResolver = new CamelCasePropertyNamesContractResolver()
                };
                var json = JsonConvert.SerializeObject(new CdnParam
                {
                    CertificateSourceParameters = new CertSource
                    {
                        ResourceGroupName = _resourceGroupName,
                        SecretName = cert.Name,
                        SecretVersion = cert.Version,
                        SubscriptionId = _azureHelper.GetSubscriptionId(),
                        VaultName = cert.Store.Name
                    }
                }, settings);
                var content = new StringContent(json, Encoding.ASCII, "application/json");
                return httpClient.PostAsync(url, content);
            }));

            foreach (var r in results)
            {
                var content = await r.Content.ReadAsStringAsync();
                r.EnsureSuccessStatusCode();
                // would now have to query this URL until operation completed successfully
                // but it may take up to 6h, so just ignore
                //var queryUrl = r.Headers.Location;
                //while (queryUrl != null)
                //{
                //    var resp = await httpClient.GetAsync(queryUrl);
                //    if (resp.StatusCode != HttpStatusCode.Accepted)
                //        break;
                //}
            }
        }

        private class CdnParam
        {
            [JsonProperty("certificateSource")]
            public string CertificateSource => "AzureKeyVault";

            [JsonProperty("protocolType")]
            public string ProtocolType => "ServerNameIndication";

            [JsonProperty("certificateSourceParameters")]
            public CertSource CertificateSourceParameters { get; set; }
        }

        private class CertSource
        {
            [JsonProperty("@odata.type")]
            public string Type => "#Microsoft.Azure.Cdn.Models.KeyVaultCertificateSourceParameters";

            [JsonProperty("resourceGroupName")]
            public string ResourceGroupName { get; set; }

            [JsonProperty("SecretName")]
            public string SecretName { get; set; }

            [JsonProperty("SecretVersion")]
            public string SecretVersion { get; set; }

            [JsonProperty("subscriptionId")]
            public string SubscriptionId { get; set; }

            [JsonProperty("vaultName")]
            public string VaultName { get; set; }

            [JsonProperty("updateRule")]
            public string UpdateRule => "NoAction";

            [JsonProperty("deleteRule")]
            public string DeleteRule => "NoAction";
        }
    }
}
