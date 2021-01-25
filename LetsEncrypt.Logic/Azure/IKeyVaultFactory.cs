using Azure.Security.KeyVault.Certificates;
using Azure.Security.KeyVault.Secrets;

namespace LetsEncrypt.Logic.Azure
{
    public interface IKeyVaultFactory
    {
        SecretClient CreateSecretClient(string keyVaultName);

        CertificateClient CreateCertificateClient(string keyVaultName);
    }
}
