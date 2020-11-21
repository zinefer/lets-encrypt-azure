using Azure;
using Azure.Security.KeyVault.Certificates;
using LetsEncrypt.Logic.Azure;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LetsEncrypt.Logic.Providers.CertificateStores
{
    public class KeyVaultCertificateStore : ICertificateStore
    {
        private readonly CertificateClient _certificateClient;
        private readonly string _certificateName;
        private readonly string _resourceGroupName;
        private readonly IAzureHelper _azureHelper;

        public KeyVaultCertificateStore(
            IAzureHelper azureHelper,
            IKeyVaultFactory keyVaultFactory,
            string keyVaultName,
            string resourceGroupName,
            string certificateName)
        {
            _azureHelper = azureHelper ?? throw new ArgumentNullException(nameof(azureHelper));
            Name = keyVaultName ?? throw new ArgumentNullException(nameof(keyVaultName));
            _resourceGroupName = resourceGroupName ?? throw new ArgumentNullException(nameof(resourceGroupName));
            _certificateName = certificateName ?? throw new ArgumentNullException(nameof(certificateName));

            // needs to be a new client as it could be a different keyvault each time
            _certificateClient = keyVaultFactory.CreateCertificateClient(keyVaultName);
        }

        public string Name { get; }

        public string Type => "keyVault";

        public string ResourceId => $"/subscriptions/{_azureHelper.GetSubscriptionId()}/resourceGroups/{_resourceGroupName}/providers/Microsoft.KeyVault/vaults/{Name}";

        public async Task<ICertificate> GetCertificateAsync(CancellationToken cancellationToken)
        {
            try
            {
                var cert = await _certificateClient.GetCertificateAsync(_certificateName, cancellationToken);
                return new CertificateInfo(cert.Value, this);
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                return null;
            }
        }

        public async Task<string[]> GetCertificateThumbprintsAsync(CancellationToken cancellationToken)
        {
            var thumbprints = new List<string>();
            await foreach (var cert in _certificateClient.GetPropertiesOfCertificateVersionsAsync(_certificateName, cancellationToken))
            {
                thumbprints.Add(ThumbprintHelper.Convert(cert.X509Thumbprint));
            }
            return thumbprints.ToArray();
        }

        public async Task<ICertificate> UploadAsync(byte[] pfxBytes, string password, string[] hostNames, CancellationToken cancellationToken)
        {
            var r = await ImportCertificateAsync(pfxBytes, password, cancellationToken);
            return new CertificateInfo(r, this);
        }

        private async Task<KeyVaultCertificateWithPolicy> ImportCertificateAsync(
            byte[] certificate,
            string password,
            CancellationToken cancellationToken)
        {
            var options = new ImportCertificateOptions(_certificateName, certificate)
            {
                Password = password,
                Enabled = true
            };
            var result = await _certificateClient.ImportCertificateAsync(options, cancellationToken);
            return result.Value;
        }
    }
}
