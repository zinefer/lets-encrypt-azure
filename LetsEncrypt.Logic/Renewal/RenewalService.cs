using Certes;
using Certes.Acme;
using Certes.Acme.Resource;
using LetsEncrypt.Logic.Acme;
using LetsEncrypt.Logic.Authentication;
using LetsEncrypt.Logic.Config;
using LetsEncrypt.Logic.Extensions;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.KeyVault.Models;
using Microsoft.Azure.Management.Cdn;
using Microsoft.Azure.Management.Cdn.Models;
using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace LetsEncrypt.Logic.Renewal
{
    public class RenewalService : IRenewalService
    {
        private readonly IChallengeService _challengeService;
        private readonly IKeyVaultClient _keyVaultClient;
        private readonly ICdnManagementClient _cdnClient;

        public RenewalService(IChallengeService challengeService,
            IKeyVaultClient keyVaultClient,
            ICdnManagementClient cdnClient)
        {
            _challengeService = challengeService ?? throw new ArgumentNullException(nameof(challengeService));
            _keyVaultClient = keyVaultClient ?? throw new ArgumentNullException(nameof(keyVaultClient));
            _cdnClient = cdnClient ?? throw new ArgumentNullException(nameof(cdnClient));
        }

        public async Task<byte[]> RenewCertificateAsync(
            CertificateConfiguration certificateConfiguration,
            AuthenticationContext authenticationContext,
            CancellationToken cancellationToken)
        {
            if (certificateConfiguration == null)
                throw new ArgumentNullException(nameof(certificateConfiguration));
            if (authenticationContext == null)
                throw new ArgumentNullException(nameof(authenticationContext));

            if (certificateConfiguration.HostNames.IsNullOrEmpty())
                throw new ArgumentNullException(nameof(certificateConfiguration.HostNames));

            var order = await authenticationContext.AcmeContext.NewOrder(certificateConfiguration.HostNames);

            // run Let's Encrypt challenge
            await ValidateOrderAsync(order, cancellationToken);

            // request certificate
            var key = KeyFactory.NewKey(KeyAlgorithm.RS256);
            await order.Finalize(new CsrInfo(), key);

            var certChain = await order.Download();
            var builder = certChain.ToPfx(key);
            builder.FullChain = true;
            var password = "";
            var pfxBytes = builder.Build(string.Join(";", certificateConfiguration.HostNames), password);

            var secretVersion = await CreateOrUpdateCertificateInKeyVaultAsync(certificateConfiguration, pfxBytes, password);

            await UpdateCdnAsync(certificateConfiguration, secretVersion);
            return null;
        }

        private async Task UpdateCdnAsync(CertificateConfiguration cfg, string secretVersion)
        {
            // https://stackoverflow.com/a/56147987
            var customDomainParameters = new UserManagedHttpsParameters("ServerNameIndication",
                new KeyVaultCertificateSourceParameters(cfg.KeyVaultSubscriptionId, cfg.KeyVaultSubscriptionId, cfg.KeyVaultName, cfg.CertificateName, secretVersion));
            var results = await Task.WhenAll(cfg.CdnDetails.Select(c =>
                _cdnClient.CustomDomains.EnableCustomHttpsWithHttpMessagesAsync(c.ResourceGroupName, c.CdnName, c.EndpointName, c.HostName, customDomainParameters)));
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
    }
}
