using Azure.Core;
using Azure.Security.KeyVault.Certificates;
using Azure.Security.KeyVault.Secrets;
using System;

namespace LetsEncrypt.Logic.Azure
{
    public class KeyVaultFactory : IKeyVaultFactory
    {
        private readonly TokenCredential _tokenCredential;

        public KeyVaultFactory(TokenCredential tokenCredential)
        {
            _tokenCredential = tokenCredential;
        }

        public SecretClient CreateSecretClient(string keyVaultName)
        {
            return new SecretClient(new Uri($"https://{keyVaultName}.vault.azure.net"), _tokenCredential);
        }

        public CertificateClient CreateCertificateClient(string keyVaultName)
        {
            return new CertificateClient(new Uri($"https://{keyVaultName}.vault.azure.net"), _tokenCredential);
        }
    }
}
