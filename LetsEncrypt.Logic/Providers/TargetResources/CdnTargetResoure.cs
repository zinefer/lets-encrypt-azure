using LetsEncrypt.Logic.Azure;
using LetsEncrypt.Logic.Extensions;
using LetsEncrypt.Logic.Providers.CertificateStores;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LetsEncrypt.Logic.Providers.TargetResources
{
    public class CdnTargetResoure : ITargetResource
    {
        private readonly string _resourceGroupName;
        private readonly string[] _endpoints;
        private readonly ILogger _logger;
        private readonly IAzureCdnClient _azureCdnClient;

        public CdnTargetResoure(
            IAzureCdnClient azureCdnClient,
            string resourceGroupName,
            string name,
            string[] endpoints,
            ILogger<CdnTargetResoure> logger)
        {
            _azureCdnClient = azureCdnClient;
            _resourceGroupName = resourceGroupName ?? throw new ArgumentNullException(nameof(resourceGroupName));
            Name = name ?? throw new ArgumentNullException(nameof(name));
            _endpoints = endpoints ?? throw new ArgumentNullException(nameof(endpoints));
            _logger = logger;
        }

        public string Name { get; }

        public string Type => "CDN";

        public bool SupportsCertificateCheck => false;

        public Task<bool> IsUsingCertificateAsync(ICertificate cert, CancellationToken cancellationToken)
        {
            // API does not return information about currently rolled out cert
            // additionally POP rollout takes ~6h and during that timeframe the old cert needs to be active
            throw new NotSupportedException("CDN does not support getting currently rolled out certificate!");
        }

        public async Task UpdateAsync(ICertificate cert, CancellationToken cancellationToken)
        {
            if (cert.Store.Type != "keyVault")
                throw new NotSupportedException("Azure CDN can only use certificates from store keyVault. Found: " + cert.Store.Type);

            // CDN seems to not like certs that have just been uploaded
            // checking if waiting a long time fixes the issue
            // note that overall max execution time is 10min
            // TODO: might have to use durable functions to make this scale with many renewals
            _logger.LogInformation("Waiting 2 minutes before deploying certificate to CDN");
            await Task.Delay(TimeSpan.FromMinutes(2), cancellationToken);

            var endpoints = await _azureCdnClient.ListEndpointsAsync(_resourceGroupName, Name, cancellationToken);
            var matchingEndpoints = endpoints
                .Where(endpoint => _endpoints.Contains(endpoint.Name, StringComparison.OrdinalIgnoreCase) &&
                            endpoint.CustomDomains.Any(domain =>
                                cert.HostNames.Contains(domain.HostName, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            var results = await _azureCdnClient.UpdateEndpointsAsync(_resourceGroupName, Name, endpoints, cert, cancellationToken);

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
    }
}
