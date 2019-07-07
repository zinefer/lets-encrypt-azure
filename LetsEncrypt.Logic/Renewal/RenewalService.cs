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
using System.Threading;
using System.Threading.Tasks;

namespace LetsEncrypt.Logic.Renewal
{
    public class RenewalService : IRenewalService
    {
        private readonly ILogger _log;
        private readonly IAuthenticationService _authenticationService;

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
            if (!await IsRenewalRequiredAsync(options, cfg, cancellationToken))
                return RenewalResult.NoChange;

            // 2. run Let's Encrypt challenge
            _log.LogInformation($"Issuing a new certificate for {hostNames}");
            var order = await ValidateOrderAsync(options, cfg, cancellationToken);

            // 3. save certificate
            var cert = await GenerateAndStoreCertificateAsync(order, cfg, cancellationToken);

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
        /// <returns>True if must be renewed, false otherwise</returns>
        private async Task<bool> IsRenewalRequiredAsync
            (IAcmeOptions options,
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
                    _log.LogInformation($"Certificate {existingCert.Name} (provider: {cfg.CertificateStore.Type}) is still valid until {existingCert.Expires.Value}. Skipping renewal.");
                    return false;
                }
            }
            // either no cert or expired
            return true;
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
                var responses = await ValidateDomainOwnershipAsync(challengeContexts, cancellationToken);
                ThrowIfNotInStatus(ChallengeStatus.Valid, responses);
            }
            finally
            {
                await challenge.CleanupAsync(challengeContexts, cancellationToken);
            }
            return order;
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
            // request certificate
            var key = KeyFactory.NewKey(KeyAlgorithm.RS256);
            await order.Finalize(new CsrInfo(), key);

            var certChain = await order.Download();
            var builder = certChain.ToPfx(key);
            builder.FullChain = true;
            var password = "";
            var pfxBytes = builder.Build(string.Join(";", cfg.HostNames), password);

            var store = cfg.ParseCertificateStore();

            return await store.UploadAsync(pfxBytes, password, cfg.HostNames, cancellationToken);
        }
    }
}
