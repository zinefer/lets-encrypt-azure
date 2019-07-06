using Certes;
using LetsEncrypt.Logic.Acme;
using LetsEncrypt.Logic.Config;
using LetsEncrypt.Logic.Storage;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LetsEncrypt.Logic.Authentication
{
    public class AuthenticationService : IAuthenticationService
    {
        private readonly IStorageProvider _storageProvider;
        private const string AccountKeyFilenamePattern = "{0}--{1}.pem";
        private readonly IAcmeContextFactory _contextFactory;
        private readonly IAcmeKeyFactory _keyFactory;

        public AuthenticationService(
            IStorageProvider storageProvider,
            IAcmeContextFactory contextFactory = null,
            IAcmeKeyFactory keyFactory = null)
        {
            // TODO: use keyvault storage
            _storageProvider = storageProvider ?? throw new ArgumentNullException(nameof(storageProvider));
            _contextFactory = contextFactory ?? new AcmeContextFactory();
            _keyFactory = keyFactory ?? new AcmeKeyFactory();
        }

        public async Task<AuthenticationContext> AuthenticateAsync(
            IAcmeOptions options,
            CancellationToken cancellationToken)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            IAcmeContext acme;
            var existingKey = await LoadExistingAccountKey(options, cancellationToken);
            if (existingKey == null)
            {
                acme = _contextFactory.GetContext(options.CertificateAuthorityUri);
                // as far as I understand there is a penalty for calling NewAccount too often
                // thus storing the key is encouraged
                // however a keyloss is "non critical" as NewAccount can be called on any existing account without problems
                await acme.NewAccount(options.Email, true);
                existingKey = acme.AccountKey;
                await StoreAccountKeyAsync(options, existingKey, cancellationToken);
            }
            else
            {
                acme = _contextFactory.GetContext(options.CertificateAuthorityUri, existingKey);
            }
            return new AuthenticationContext(acme, options);
        }

        private async Task<IKey> LoadExistingAccountKey(
            IAcmeOptions options,
            CancellationToken cancellationToken)
        {
            var fileName = GetAccountKeyFilename(options);

            if (!await _storageProvider.ExistsAsync(fileName, cancellationToken))
                return null;

            var content = await _storageProvider.GetAsync(fileName, cancellationToken);
            return _keyFactory.FromPem(content);
        }

        private Task StoreAccountKeyAsync(
            IAcmeOptions options,
            IKey existingKey,
            CancellationToken cancellationToken)
        {
            var filename = GetAccountKeyFilename(options);
            var content = existingKey.ToPem();
            return _storageProvider.SetAsync(filename, content, cancellationToken);
        }

        private string GetAccountKeyFilename(IAcmeOptions options)
            => _storageProvider.Escape(string.Format(AccountKeyFilenamePattern, options.CertificateAuthorityUri.Host, options.Email));
    }
}
