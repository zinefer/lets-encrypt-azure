using LetsEncrypt.Logic.Azure;
using LetsEncrypt.Logic.Azure.Response;
using LetsEncrypt.Logic.Extensions;
using LetsEncrypt.Logic.Providers.CertificateStores;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
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

        public bool SupportsCertificateCheck => true;

        public async Task<bool> IsUsingCertificateAsync(ICertificate cert, CancellationToken cancellationToken)
        {
            // API does not return information about currently rolled out cert
            var endpoints = await MatchEndpointsAsync(cert, cancellationToken);
            var inProgress = new Dictionary<CdnResponse, List<CdnCustomDomainResponse>>();
            foreach (var endpoint in endpoints)
            {
                // if any cert deployment is in process then assume it's for the correct cert
                // since cert deployment takes 6h anyway there's nothing to do but wait
                // => worst case we do one endpoint per day (if only one deployment succeeds and the rest fail)
                var customDomainDetails = await _azureCdnClient.GetCustomDomainDetailsAsync(_resourceGroupName, Name, endpoint, cancellationToken);
                // filter to only relevant domains, we don't care about other deployments in progress as they don't affect us (for this particular cert anyway)
                var deploymentsInProgress = customDomainDetails
                    .Where(x => cert.HostNames.Contains(x.HostName, StringComparison.OrdinalIgnoreCase) &&
                                x.CustomHttpsProvisioningState == CustomHttpsProvisioningState.Enabling)
                    .ToList();
                if (deploymentsInProgress.Any())
                {
                    if (!inProgress.ContainsKey(endpoint))
                        inProgress.Add(endpoint, new List<CdnCustomDomainResponse>());
                    inProgress[endpoint].AddRange(deploymentsInProgress);
                }
                else
                {
                    // if non are in progress check rolled out cert
                    foreach (var details in customDomainDetails)
                    {
                        if (details.CustomHttpsProvisioningState != CustomHttpsProvisioningState.Enabled ||
                            details.CustomHttpsProvisioningSubstate != CustomHttpsProvisioningSubstate.CertificateDeployed ||
                            !IsCertificateFromSecretVersion(cert, details.CustomHttpsParameters.CertificateSourceParameters.SecretVersion))
                        {
                            _logger.LogWarning($"Wrong certificate is rolled out on (endpoint: {endpoint.Name}, domain: {details.HostName}). Expected certificate version: {cert.CertificateVersion}, found: {details.CustomHttpsParameters?.CertificateSourceParameters?.SecretVersion ?? "null"}. Provisioning status was: {details.CustomHttpsProvisioningState}, sub state: {details.CustomHttpsProvisioningSubstate}");
                            return false;
                        }
                    }
                }
            }
            if (inProgress.Any())
            {
                // must wait for cert deployment
                // as in-progress deployments cannot be canceled and cause all further API calls to return with 400 BadRequest
                _logger.LogWarning($"Certificate deployment on endpoint is still in progress! Cannot update certificate now -> retrying next time. Deployment in progress for endpoints: {Environment.NewLine}" +
                    string.Join(Environment.NewLine, inProgress.Select(pair =>
                    {
                        var endpoint = pair.Key;
                        var domains = pair.Value;
                        return $" {endpoint.Name} (domains: {string.Join(", ", domains.Select(x => x.HostName))})";
                    })));

                // "half truth". we don't know yet which cert is being rolled out
                // so assume it's the correct one
                // once it's rolled out we can check the cert thumbprint (-> next run)
            }

            // correct cert is in use
            return true;
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

            var endpoints = await MatchEndpointsAsync(cert, cancellationToken);

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

        private async Task<CdnResponse[]> MatchEndpointsAsync(ICertificate cert, CancellationToken cancellationToken)
        {
            var endpoints = await _azureCdnClient.ListEndpointsAsync(_resourceGroupName, Name, cancellationToken);
            var matchingEndpoints = endpoints
                .Where(endpoint => _endpoints.Contains(endpoint.Name, StringComparison.OrdinalIgnoreCase) &&
                            endpoint.CustomDomains.Any(domain =>
                                cert.HostNames.Contains(domain.HostName, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            return matchingEndpoints.ToArray();
        }

        private bool IsCertificateFromSecretVersion(ICertificate cert, string secretVersion)
        {
            // no need to fetch from keyvault only to compare thumbprints. comparing secret version achieves the same guarantee
            return cert.CertificateVersion.Equals(secretVersion, StringComparison.OrdinalIgnoreCase);
        }
    }
}
