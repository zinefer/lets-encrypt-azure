using Microsoft.Azure.KeyVault;
using Microsoft.Azure.KeyVault.Models;
using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace LetsEncrypt.Logic.Providers.CertificateStores
{
    public class KeyVaultCertificateStore : ICertificateStore
    {
        private readonly IKeyVaultClient _keyVaultClient;
        private readonly string _certificateName;

        public KeyVaultCertificateStore(IKeyVaultClient keyVaultClient,
            string keyVaultName,
            string certificateName)
        {
            _keyVaultClient = keyVaultClient ?? throw new ArgumentNullException(nameof(keyVaultClient));
            Name = keyVaultName ?? throw new ArgumentNullException(nameof(keyVaultClient));
            _certificateName = certificateName ?? throw new ArgumentNullException(nameof(keyVaultClient));
        }

        public string Name { get; }

        public async Task<ICertificate> GetCertificateAsync(CancellationToken cancellationToken)
        {
            try
            {
                var cert = await _keyVaultClient.GetCertificateAsync($"https://{Name}.vault.azure.net", _certificateName, cancellationToken);
                return new CertificateInfo(cert, Name);
            }
            catch (KeyVaultErrorException ex)
            {
                if (ex.Response.StatusCode == System.Net.HttpStatusCode.NotFound ||
                    ex.Body.Error.Code == "CertificateNotFound")
                    return null;

                throw;
            }
        }

        public async Task<ICertificate> UploadAsync(byte[] pfxBytes, string password, string[] hostNames, CancellationToken cancellationToken)
        {
            var cert = LoadFrom(pfxBytes, password);
            var base64 = Convert.ToBase64String(pfxBytes);
            var now = DateTime.UtcNow;
            var attr = new CertificateAttributes(true, now, cert.NotAfter, now);
            var r = await _keyVaultClient.ImportCertificateAsync($"https://{Name}.vault.azure.net", _certificateName, base64, password, certificateAttributes: attr);

            return new CertificateInfo(r, Name);
        }

        private X509Certificate2 LoadFrom(byte[] bytes, string password)
        {
            // must use collection instead of ctor(string filePath), otherwise exceptions are thrown.
            // https://stackoverflow.com/questions/44053426/cannot-find-the-requested-object-exception-while-creating-x509certificate2-fro/44073265#44073265
            var collection = new X509Certificate2Collection();
            if (string.IsNullOrEmpty(password))
            {
                collection.Import(bytes, null, X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);
            }
            else
            {
                // https://stackoverflow.com/a/9984307
                collection.Import(bytes, password, X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);
            }

            return collection[0];
        }
    }
}
