using Certes;
using Certes.Acme;
using LetsEncrypt.Logic.Storage;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LetsEncrypt.Logic.Acme
{
    /// <summary>
    /// Implementation that persists http challenge files in an azure blob storage.
    /// Intended for static websites hosted in azure storage via '$web' container.
    /// </summary>
    public class AzureStorageHttpChallengeService : IChallengeService
    {
        private readonly IStorageProvider _storageProvider;

        public AzureStorageHttpChallengeService(IStorageProvider storageProvider)
        {
            _storageProvider = storageProvider ?? throw new ArgumentNullException(nameof(storageProvider));
        }

        public async Task<IChallengeContext[]> InitiateChallengesAsync(IOrderContext order, CancellationToken cancellationToken)
        {
            var auth = await order.Authorizations();
            var challengeContexts = await Task.WhenAll(auth.Select(a => a.Http()));

            var wellKnownChallengeFiles = challengeContexts
                .Select(c => (c.Token, c.KeyAuthz))
                .ToArray();

            await PersistFileChallengeAsync(wellKnownChallengeFiles, cancellationToken);

            return challengeContexts;
        }

        public Task CleanupAsync(IChallengeContext[] challengeContexts, CancellationToken cancellationToken)
        {
            return Task.WhenAll(challengeContexts.Select(c => _storageProvider.DeleteAsync($".well-known/acme-challenge/{c.Token}", cancellationToken)));
        }

        private Task PersistFileChallengeAsync((string fileName, string content)[] wellKnownChallengeFiles, CancellationToken cancellationToken)
        {
            return Task.WhenAll(wellKnownChallengeFiles.Select(c => _storageProvider.SetAsync($".well-known/acme-challenge/{c.fileName}", c.content, cancellationToken)));
        }
    }
}
