using Certes;
using Certes.Acme;
using Certes.Acme.Resource;
using LetsEncrypt.Logic.Authentication;
using LetsEncrypt.Logic.Config;
using LetsEncrypt.Logic.Extensions;
using LetsEncrypt.Logic.Providers.CertificateStores;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace LetsEncrypt.Logic
{
    public class RenewalService : IRenewalService
    {
        private readonly ILogger _log;
        private readonly IAuthenticationService _authenticationService;
        private static readonly RNGCryptoServiceProvider _randomGenerator = new RNGCryptoServiceProvider();

        public RenewalService(
            IAuthenticationService authenticationService,
            ILogger log)
        {
            _authenticationService = authenticationService ?? throw new ArgumentNullException(nameof(authenticationService));
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public async Task<RenewalResult> RenewCertificateAsync(
            IAcmeOptions options,
            ICertificateRenewalOptions cfg,
            CancellationToken cancellationToken)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));
            if (cfg == null)
                throw new ArgumentNullException(nameof(cfg));

            var hostNames = string.Join(";", cfg.HostNames);
            _log.LogInformation($"Working on certificate for: {hostNames}");

            // 1. skip if not outdated yet
            var cert = await GetExistingCertificateAsync(options, cfg, cancellationToken);

            // TODO: this also skips the resourceUpdate (needed in case of previous errors or incase of cert already existing) -> config flag?
            if (cert != null)
                return RenewalResult.NoChange;

            // 2. run Let's Encrypt challenge
            _log.LogInformation($"Issuing a new certificate for {hostNames}");
            var order = await ValidateOrderAsync(options, cfg, cancellationToken);

            // 3. save certificate
            cert = await GenerateAndStoreCertificateAsync(order, cfg, cancellationToken);

            // 4. update Azure resource
            var resource = cfg.ParseTargetResource();
            await resource.UpdateAsync(cert, cancellationToken);

            return RenewalResult.Success;
        }

        /// <summary>
        /// Checks if we have to renew the specific certificate just yet.
        /// </summary>
        /// <param name="options"></param>
        /// <param name="cfg"></param>
        /// <param name="cancellationToken"></param>
        /// <returns>The cert if it is still valid according to rules in config. False otherwise.</returns>
        private async Task<ICertificate> GetExistingCertificateAsync(
            IAcmeOptions options,
            ICertificateRenewalOptions cfg,
            CancellationToken cancellationToken)
        {
            var cert = cfg.ParseCertificateStore();

            // determine if renewal is needed based on existing cert
            var existingCert = await cert.GetCertificateAsync(cancellationToken);

            // check if cert exists and that it is valid right now
            if (existingCert != null)
            {
                // handle cases of manually uploaded certificates
                if (!existingCert.Expires.HasValue)
                    throw new NotSupportedException($"Missing expiration value on certificate {existingCert.Name} (provider: {cfg.CertificateStore.Type}). " +
                        "Must be set to expiration date of the certificate.");

                var now = DateTime.UtcNow;
                // must be valid now and some day in the future based on config expiration rule
                var isValid =
                    (!existingCert.NotBefore.HasValue || existingCert.NotBefore < now) &&
                    existingCert.Expires.Value.AddDays(-options.RenewXDaysBeforeExpiry) > now;
                if (isValid)
                {
                    _log.LogInformation($"Certificate {existingCert.Name} (from source: {cert.Name}) is still valid until {existingCert.Expires.Value}. Skipping renewal.");
                    return existingCert;
                }
            }
            // either no cert or expired
            return null;
        }

        /// <summary>
        /// Runs the LetsEncrypt challenge and verifies that it was completed successfully.
        /// </summary>
        /// <param name="options"></param>
        /// <param name="cfg"></param>
        /// <param name="cancellationToken"></param>
        /// <returns>Returns the context, ready to generate certificate from.</returns>
        private async Task<IOrderContext> ValidateOrderAsync(
            IAcmeOptions options,
            ICertificateRenewalOptions cfg,
            CancellationToken cancellationToken)
        {
            var authenticationContext = await _authenticationService.AuthenticateAsync(options, cancellationToken);
            var order = await authenticationContext.AcmeContext.NewOrder(cfg.HostNames);

            var challenge = await cfg.ParseChallengeResponderAsync(cancellationToken);

            var challengeContexts = await challenge.InitiateChallengesAsync(order, cancellationToken);

            if (challengeContexts.IsNullOrEmpty())
                throw new ArgumentNullException(nameof(challengeContexts));

            try
            {
                // validate domain ownership
                await ValidateDomainOwnershipAsync(challengeContexts, cancellationToken);
            }
            finally
            {
                await challenge.CleanupAsync(challengeContexts, cancellationToken);
            }
            return order;
        }

        private async Task ValidateDomainOwnershipAsync(IChallengeContext[] challengeContexts, CancellationToken cancellationToken)
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

        /// <summary>
        /// Creates a valid certificate from the order and uploads it to the store.
        /// </summary>
        /// <param name="order"></param>
        /// <param name="cfg"></param>
        /// <param name="cancellationToken"></param>
        /// <returns>Returns metadata about the certificate.</returns>
        private async Task<ICertificate> GenerateAndStoreCertificateAsync(
            IOrderContext order,
            ICertificateRenewalOptions cfg,
            CancellationToken cancellationToken)
        {
            var store = cfg.ParseCertificateStore();
            _log.LogInformation($"Storing certificate in {store.Name}");

            // request certificate
            var key = KeyFactory.NewKey(KeyAlgorithm.RS256);
            await order.Finalize(new CsrInfo(), key);

            var certChain = await order.Download();
            var builder = certChain.ToPfx(key);
            builder.FullChain = true;

            var bytes = new byte[32];
            _randomGenerator.GetNonZeroBytes(bytes);
            var password = Convert.ToBase64String(bytes);
            var pfxBytes = builder.Build(string.Join(";", cfg.HostNames), password);

            return await store.UploadAsync(pfxBytes, password, cfg.HostNames, cancellationToken);
        }
    }
}
