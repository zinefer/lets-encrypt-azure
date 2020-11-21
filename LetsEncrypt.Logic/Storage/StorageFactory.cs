using Azure.Core;
using System.Threading;
using System.Threading.Tasks;

namespace LetsEncrypt.Logic.Storage
{
    public class StorageFactory : IStorageFactory
    {
        private readonly TokenCredential _tokenCredential;

        public StorageFactory(
            TokenCredential tokenCredential)
        {
            _tokenCredential = tokenCredential;
        }

        public IStorageProvider FromConnectionString(string connectionString, string containerName)
        {
            return new AzureBlobStorageProvider(connectionString, containerName);
        }

        public Task<IStorageProvider> FromMsiAsync(string accountName, string containerName, CancellationToken cancellationToken)
        {
            IStorageProvider provider = new AzureBlobStorageProvider(_tokenCredential, accountName, containerName);

            return Task.FromResult(provider);
        }
    }
}
