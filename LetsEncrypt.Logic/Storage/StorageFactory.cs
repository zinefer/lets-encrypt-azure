using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.WindowsAzure.Storage.Auth;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LetsEncrypt.Logic.Storage
{
    public class StorageFactory : IStorageFactory
    {
        private readonly IAzureHelper _azureHelper;

        public StorageFactory(IAzureHelper azureHelper)
        {
            _azureHelper = azureHelper ?? throw new ArgumentNullException(nameof(azureHelper));
        }

        public IStorageProvider FromConnectionString(string connectionString, string containerName)
        {
            return new AzureBlobStorageProvider(connectionString, containerName);
        }

        public async Task<IStorageProvider> FromMsiAsync(string accountName, string containerName, CancellationToken cancellationToken)
        {
            var provider = new AzureServiceTokenProvider();
            var tokenAndFrequency = await StorageMsiTokenRenewerAsync(provider, cancellationToken);
            var token = new TokenCredential(tokenAndFrequency.Token, StorageMsiTokenRenewerAsync, provider, tokenAndFrequency.Frequency.Value);
            return new AzureBlobStorageProvider(token, accountName, containerName);
        }

        private async Task<NewTokenAndFrequency> StorageMsiTokenRenewerAsync(object state, CancellationToken cancellationToken)
        {
            var provider = (AzureServiceTokenProvider)state;
            const string StorageResource = "https://storage.azure.com/";
            var authResult = await provider.GetAuthenticationResultAsync(StorageResource, await _azureHelper.GetTenantIdAsync(cancellationToken), cancellationToken);
            var next = authResult.ExpiresOn - DateTimeOffset.UtcNow - TimeSpan.FromMinutes(5);
            if (next.Ticks < 0)
                next = default;

            return new NewTokenAndFrequency(authResult.AccessToken, next);
        }
    }
}
