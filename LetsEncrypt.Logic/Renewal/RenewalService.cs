using Certes;
using Certes.Acme;
using Certes.Acme.Resource;
using LetsEncrypt.Logic.Acme;
using LetsEncrypt.Logic.Authentication;
using LetsEncrypt.Logic.Config;
using LetsEncrypt.Logic.Extensions;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.KeyVault.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LetsEncrypt.Logic.Renewal
{
    public class RenewalService : IRenewalService
    {
        private readonly IChallengeService _challengeService;
        private readonly IKeyVaultClient _keyVaultClient;
        private readonly HttpClient _httpClient;
        private readonly ILogger _log;

        public RenewalService(IChallengeService challengeService,
            IKeyVaultClient keyVaultClient,
            HttpClient httpClient,
            ILogger log)
        {
            _challengeService = challengeService ?? throw new ArgumentNullException(nameof(challengeService));
            _keyVaultClient = keyVaultClient ?? throw new ArgumentNullException(nameof(keyVaultClient));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public async Task<RenewalResult> RenewCertificateAsync(
            CertificateConfiguration cfg,
            AuthenticationContext authenticationContext,
            CancellationToken cancellationToken)
        {
            if (cfg == null)
                throw new ArgumentNullException(nameof(cfg));
            if (authenticationContext == null)
                throw new ArgumentNullException(nameof(authenticationContext));

            if (cfg.HostNames.IsNullOrEmpty())
                throw new ArgumentNullException(nameof(cfg.HostNames));

            var order = await authenticationContext.AcmeContext.NewOrder(cfg.HostNames);

            // determine if renewal is needed based on existing cert
            var existingCert = await GetCertificateAsync(cfg.KeyVaultName, cfg.CertificateName, cancellationToken);
            // check if cert exists and that it is valid right now
            if (existingCert != null)
            {
                // handle cases of manually uploaded certificates
                if (!existingCert.Attributes.Expires.HasValue)
                    throw new NotSupportedException($"Missing expiration value on certificate {cfg.CertificateName} in keyvault {cfg.KeyVaultName}. Must be set to expiration date of the certificate.");

                var now = DateTime.UtcNow;
                // must be valid now and 
                var isValid =
                    (!existingCert.Attributes.NotBefore.HasValue || existingCert.Attributes.NotBefore < now) &&
                    existingCert.Attributes.Expires.Value.AddDays(-cfg.ReneweXDaysBeforeExpiry) > now;
                if (isValid)
                {
                    _log.LogInformation($"Certificate {cfg.CertificateName} in keyvault {cfg.KeyVaultName} still valid until {existingCert.Attributes.Expires.Value}. Skipping renewal.");
                    return RenewalResult.NoChange;
                }
            }

            // run Let's Encrypt challenge
            await ValidateOrderAsync(order, cancellationToken);

            // request certificate
            var key = KeyFactory.NewKey(KeyAlgorithm.RS256);
            await order.Finalize(new CsrInfo(), key);

            var certChain = await order.Download();
            var builder = certChain.ToPfx(key);
            builder.FullChain = true;
            var password = "";
            var pfxBytes = builder.Build(string.Join(";", cfg.HostNames), password);

            var secretVersion = await CreateOrUpdateCertificateInKeyVaultAsync(cfg, pfxBytes, password);

            await UpdateCdnAsync(cfg, secretVersion);
            return RenewalResult.Success;
        }

        private async Task<CertificateBundle> GetCertificateAsync(string keyVaultName, string certificateName, CancellationToken cancellationToken)
        {
            try
            {
                return await _keyVaultClient.GetCertificateAsync($"https://{keyVaultName}.vault.azure.net", certificateName, cancellationToken);
            }
            catch (KeyVaultErrorException ex)
            {
                if (ex.Response.StatusCode == System.Net.HttpStatusCode.NotFound ||
                    ex.Body.Error.Code == "CertificateNotFound")
                    return null;

                throw;
            }
        }

        private async Task UpdateCdnAsync(CertificateConfiguration cfg, string secretVersion)
        {
            // https://stackoverflow.com/a/56147987
            // update all CDNs in parallel
            var results = await Task.WhenAll(cfg.CdnDetails.SelectMany(c =>
                c.Endpoints.Select(e =>
                {
                    // https://github.com/Azure/azure-rest-api-specs/blob/master/specification/cdn/resource-manager/Microsoft.Cdn/stable/2019-04-15/examples/CustomDomains_EnableCustomHttpsUsingBYOC.json
                    var formattedHostName = e.HostName.Replace(".", "-");
                    var url = $"https://management.azure.com/subscriptions/{cfg.KeyVaultSubscriptionId}/resourceGroups/{c.ResourceGroupName}/providers/Microsoft.Cdn/profiles/{c.CdnName}/endpoints/{e.EndpointName}/customDomains/{formattedHostName}/enableCustomHttps?api-version=2019-04-15";
                    var settings = new JsonSerializerSettings
                    {
                        Formatting = Formatting.Indented,
                        ContractResolver = new CamelCasePropertyNamesContractResolver()
                    };
                    var json = JsonConvert.SerializeObject(new CdnParam
                    {
                        CertificateSourceParameters = new CertSource
                        {
                            ResourceGroupName = c.ResourceGroupName,
                            SecretName = cfg.CertificateName,
                            SecretVersion = secretVersion,
                            SubscriptionId = cfg.KeyVaultSubscriptionId,
                            VaultName = cfg.KeyVaultName
                        }
                    }, settings);
                    var content = new StringContent(json, Encoding.ASCII, "application/json");
                    return _httpClient.PostAsync(url, content);
                })));
            foreach (var r in results)
            {
                var content = await r.Content.ReadAsStringAsync();
                r.EnsureSuccessStatusCode();
                var queryUrl = r.Headers.Location;
                while (true)
                {
                    var resp = await _httpClient.GetAsync(queryUrl);
                    if (resp.IsSuccessStatusCode)
                        break;
                }
            }
        }

        private async Task<string> CreateOrUpdateCertificateInKeyVaultAsync(CertificateConfiguration cfg, byte[] pfxBytes, string password)
        {
            var cert = new X509Certificate2(pfxBytes, password, X509KeyStorageFlags.MachineKeySet);
            var base64 = Convert.ToBase64String(pfxBytes);
            var now = DateTime.UtcNow;
            var attr = new CertificateAttributes(true, now, cert.NotAfter, now);
            var r = await _keyVaultClient.ImportCertificateAsync($"https://{cfg.KeyVaultName}.vault.azure.net", cfg.CertificateName, base64, password, certificateAttributes: attr);
            return r.SecretIdentifier.Version;
        }

        private async Task ValidateOrderAsync(
            IOrderContext order,
            CancellationToken cancellationToken)
        {
            var challengeContexts = await _challengeService.InitiateChallengesAsync(order, cancellationToken);

            if (challengeContexts.IsNullOrEmpty())
                throw new ArgumentNullException(nameof(challengeContexts));

            try
            {
                // validate domain ownership
                var responses = await ValidateDomainOwnershipAsync(challengeContexts, cancellationToken);
                ThrowIfNotInStatus(ChallengeStatus.Valid, responses);
            }
            finally
            {
                await _challengeService.CleanupAsync(challengeContexts, cancellationToken);
            }
        }

        private async Task<Challenge[]> ValidateDomainOwnershipAsync(IChallengeContext[] challengeContexts, CancellationToken cancellationToken)
        {
            // let Let's Encrypt know that they can verify the challenge
            await Task.WhenAll(challengeContexts.Select(c => c.Validate()));

            // fetch response from Let's encrypt regarding challenge success/failure
            Challenge[] challengeResponses = null;
            do
            {
                if (challengeResponses != null)
                {
                    // wait before querying again
                    await Task.Delay(500, cancellationToken);
                }
                challengeResponses = await Task.WhenAll(challengeContexts.Select(c => c.Resource()));
            }
            while (challengeResponses.Any(c =>
                c.Status == ChallengeStatus.Pending ||
                c.Status == ChallengeStatus.Processing));

            ThrowIfNotInStatus(ChallengeStatus.Valid, challengeResponses);

            return challengeResponses;
        }

        private void ThrowIfNotInStatus(ChallengeStatus expectedStatus, Challenge[] challenges)
        {
            if (challenges.IsNullOrEmpty())
                throw new ArgumentNullException(nameof(challenges));

            var failures = challenges
                .Where(c => c.Status != expectedStatus)
                .Select(f => f.Error)
                .ToArray();
            if (failures.Any())
            {
                throw new RenewalException($"Expected all challenges to be in status {expectedStatus}, but {failures.Length} where not. See exception for details.", failures);
            }
        }

        private class CdnParam
        {
            public string CertificateSource => "AzureKeyVault";

            public string ProtocolType => "ServerNameIndication";

            public CertSource CertificateSourceParameters { get; set; }
        }

        private class CertSource
        {
            [JsonProperty("@odata.type")]
            public string Type => "#Microsoft.Azure.Cdn.Models.KeyVaultCertificateSourceParameters";

            public string ResourceGroupName { get; set; }

            public string SecretName { get; set; }

            public string SecretVersion { get; set; }

            public string SubscriptionId { get; set; }

            public string VaultName { get; set; }

            public string UpdateRule => "NoAction";

            public string DeleteRule => "NoAction";
        }
    }
}
