using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LetsEncrypt.Logic.Storage
{
    public class AzureBlobStorageProvider : IStorageProvider
    {
        private readonly CloudBlobClient _blobClient;
        private readonly string _container;

        public AzureBlobStorageProvider(
            string connectionString,
            string container)
        {
            var storageClient = CloudStorageAccount.Parse(connectionString ?? throw new ArgumentNullException(nameof(connectionString)));
            _blobClient = storageClient.CreateCloudBlobClient();
            _container = container ?? throw new ArgumentNullException(nameof(container));
        }

        public string Escape(string fileName)
            => Uri.EscapeDataString(fileName);

        public async Task<bool> ExistsAsync(
            string fileName,
            CancellationToken cancellationToken)
        {
            var block = await GetBlockBlobAsync(fileName, false, cancellationToken);
            return await block.ExistsAsync(null, null, cancellationToken);
        }

        public async Task<string> ReadStringAsync(
            string fileName,
            CancellationToken cancellationToken)
        {
            var blob = await GetBlockBlobAsync(fileName, false, cancellationToken);
            return await blob.DownloadTextAsync(Encoding.UTF8, null, null, null, cancellationToken);
        }

        public async Task WriteStringAsync(
            string fileName,
            string content,
            CancellationToken cancellationToken)
        {
            var blob = await GetBlockBlobAsync(fileName, true, cancellationToken);
            await blob.UploadTextAsync(content, null, null, null, null, cancellationToken);
        }

        public async Task DeleteAsync(string fileName, CancellationToken cancellationToken)
        {
            var blob = await GetBlockBlobAsync(fileName, false, cancellationToken);
            await blob.DeleteIfExistsAsync();
        }

        private async Task<CloudBlobContainer> GetContainerAsync(
            bool createContainerIfNotExists,
            CancellationToken cancellationToken)
        {
            var container = _blobClient.GetContainerReference(_container);
            if (createContainerIfNotExists)
                await container.CreateIfNotExistsAsync(BlobContainerPublicAccessType.Off, null, null, cancellationToken);

            return container;
        }

        private async Task<CloudBlockBlob> GetBlockBlobAsync(
            string filePath,
            bool createContainerIfNotExists,
            CancellationToken cancellationToken)
        {
            var container = await GetContainerAsync(createContainerIfNotExists, cancellationToken);

            return container.GetBlockBlobReference(filePath);
        }
    }
}
