using Certes;
using Certes.Acme;
using LetsEncrypt.Logic.Storage;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LetsEncrypt.Logic.Providers.ChallengeResponders
{
    /// <summary>
    /// Implementation that persists http challenge files in an azure blob storage.
    /// Intended for static websites hosted in azure storage via '$web' container.
    /// </summary>
    public class AzureStorageHttpChallengeResponder : IChallengeResponder
    {
        private readonly IStorageProvider _storageProvider;
        private readonly string _pathPrefix;

        public AzureStorageHttpChallengeResponder(IStorageProvider storageProvider, string pathInContainer)
        {
            _storageProvider = storageProvider ?? throw new ArgumentNullException(nameof(storageProvider));
            _pathPrefix = pathInContainer ?? throw new ArgumentNullException(nameof(pathInContainer));
            if (!_pathPrefix.EndsWith("/"))
                _pathPrefix += "/";
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
            return Task.WhenAll(challengeContexts.Select(c => _storageProvider.DeleteAsync(_pathPrefix + c.Token, cancellationToken)));
        }

        private Task PersistFileChallengeAsync((string fileName, string content)[] wellKnownChallengeFiles, CancellationToken cancellationToken)
        {
            return Task.WhenAll(wellKnownChallengeFiles.Select(c => _storageProvider.SetAsync(_pathPrefix + c.fileName, c.content, cancellationToken)));
        }
    }
}
