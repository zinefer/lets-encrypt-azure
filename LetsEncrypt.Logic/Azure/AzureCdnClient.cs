using LetsEncrypt.Logic.Azure.Response;
using LetsEncrypt.Logic.Providers.CertificateStores;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LetsEncrypt.Logic.Azure
{
    public class AzureCdnClient : IAzureCdnClient
    {
        private readonly IAzureHelper _azureHelper;

        public AzureCdnClient(
            IAzureHelper azureHelper)
        {
            _azureHelper = azureHelper;
        }

        public async Task<CdnResponse[]> ListEndpointsAsync(string resourceGroupName, string name, CancellationToken cancellationToken)
        {
            // use REST directly because nuget packages don't contain the required endpoint to update CDN yet
            // fluent api would be nicer to use (mgmt api preview package already offers new endpoints, but fluent api does not)
            // but problematic: neither api supports fallback from MSI to local user (both requiring MSI_ENDPOINT env variable)
            // see https://github.com/Azure/azure-libraries-for-net/issues/585

            var httpClient = await _azureHelper.GetAuthenticatedARMClientAsync(cancellationToken);

            // actually find the endpoints which have matching certs
            // e.g. cert input "www.example.com, example.com" will have two seperate endpoints for the domains
            // user could also possibly misconfigure (include an endpoint for which we are not issuing a cert)
            // => don't want to break that either..

            var listEndpointUrl = "https://management.azure.com" +
                    $"/subscriptions/{_azureHelper.GetSubscriptionId()}/" +
                    $"resourceGroups/{resourceGroupName}/" +
                    $"providers/Microsoft.Cdn/profiles/{name}/endpoints?api-version=2019-04-15";

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

            return endpoints.value
                .Select(x => new CdnResponse
                {
                    Name = x.name,
                    CustomDomains = x.properties.customDomains
                        .Select(y => new CdnCustomDomain
                        {
                            Name = y.name,
                            HostName = y.properties.hostName
                        })
                        .ToArray()
                })
                .ToArray();
        }

        public async Task<CdnCustomDomainResponse[]> GetCustomDomainDetailsAsync(string resourceGroupName, string name, CdnResponse endpoint, CancellationToken cancellationToken)
        {
            var httpClient = await _azureHelper.GetAuthenticatedARMClientAsync(cancellationToken);
            // use REST api, management SDK doesn't have new endpoints yet (fluent SDK not at all, regular mgmt SDK only in preview release)
            var results = await Task.WhenAll(endpoint.CustomDomains.Select(async domain =>
                {
                    // https://github.com/Azure/azure-rest-api-specs/blob/master/specification/cdn/resource-manager/Microsoft.Cdn/stable/2019-04-15/examples/CustomDomains_EnableCustomHttpsUsingBYOC.json
                    // as per https://stackoverflow.com/a/56147987
                    var url = "https://management.azure.com" +
                        $"/subscriptions/{_azureHelper.GetSubscriptionId()}/" +
                        $"resourceGroups/{resourceGroupName}/" +
                        $"providers/Microsoft.Cdn/profiles/{name}/" +
                        $"endpoints/{endpoint.Name}/customDomains/" +
                        $"{domain.Name}?api-version=2019-04-15";

                    var response = await httpClient.GetAsync(url, cancellationToken);
                    response.EnsureSuccessStatusCode();
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var cdnResponse = JsonConvert.DeserializeAnonymousType(responseContent, new
                    {
                        name = "",
                        properties = new CdnCustomDomainResponse()
                    });
                    return cdnResponse;
                }));

            return results.Select(x => x.properties).ToArray();
        }

        public async Task<HttpResponseMessage[]> UpdateEndpointsAsync(string resourceGroupName, string name, CdnResponse[] endpoints, ICertificate cert, CancellationToken cancellationToken)
        {
            var httpClient = await _azureHelper.GetAuthenticatedARMClientAsync(cancellationToken);
            // use REST api, management SDK doesn't have new endpoints yet (fluent SDK not at all, regular mgmt SDK only in preview release)
            // https://stackoverflow.com/a/56147987
            // update all endpoints in parallel
            var results = await Task.WhenAll(endpoints
                .SelectMany(e => e.CustomDomains, (endpoint, domain) =>
                {
                    // https://github.com/Azure/azure-rest-api-specs/blob/master/specification/cdn/resource-manager/Microsoft.Cdn/stable/2019-04-15/examples/CustomDomains_EnableCustomHttpsUsingBYOC.json
                    // as per https://stackoverflow.com/a/56147987
                    var url = "https://management.azure.com" +
                        $"/subscriptions/{_azureHelper.GetSubscriptionId()}/" +
                        $"resourceGroups/{resourceGroupName}/" +
                        $"providers/Microsoft.Cdn/profiles/{name}/" +
                        $"endpoints/{endpoint.Name}/customDomains/" +
                        $"{domain.Name}/enableCustomHttps?api-version=2019-04-15";

                    var settings = new JsonSerializerSettings
                    {
                        Formatting = Formatting.Indented,
                        ContractResolver = new CamelCasePropertyNamesContractResolver()
                    };
                    var json = JsonConvert.SerializeObject(new CdnParam
                    {
                        CertificateSourceParameters = new CertSource
                        {
                            ResourceGroupName = resourceGroupName,
                            SecretName = cert.Name,
                            SecretVersion = cert.Version,
                            SubscriptionId = _azureHelper.GetSubscriptionId(),
                            VaultName = cert.Store.Name
                        }
                    }, settings);
                    var content = new StringContent(json, Encoding.ASCII, "application/json");
                    return httpClient.PostAsync(url, content, cancellationToken);
                }));

            return results;
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
