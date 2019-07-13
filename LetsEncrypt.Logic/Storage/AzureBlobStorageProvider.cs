using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LetsEncrypt.Logic.Storage
{
    public class AzureBlobStorageProvider : IStorageProvider
    {
        private CloudBlobClient _blobClient;
        private string _container;

        public AzureBlobStorageProvider(
            string connectionString,
            string container)
        {
            var storageClient = CloudStorageAccount.Parse(connectionString ?? throw new ArgumentNullException(nameof(connectionString)));
            Setup(storageClient, container);
        }

        public AzureBlobStorageProvider(TokenCredential msiCredentials, string account, string container)
        {
            var cred = new StorageCredentials(msiCredentials ?? throw new ArgumentNullException(nameof(msiCredentials)));
            var storageClient = new CloudStorageAccount(cred, account, null, true);
            Setup(storageClient, container);
        }

        private void Setup(CloudStorageAccount account, string container)
        {
            _blobClient = account.CreateCloudBlobClient();
            _container = container ?? throw new ArgumentNullException(nameof(container));
        }

        public string Escape(string fileName)
            => fileName;

        public async Task<bool> ExistsAsync(
            string fileName,
            CancellationToken cancellationToken)
        {
            var block = await GetBlockBlobAsync(fileName, false, cancellationToken);
            return await block.ExistsAsync(null, null, cancellationToken);
        }

        public async Task<string[]> ListAsync(string prefix, CancellationToken cancellationToken)
        {
            var container = await GetContainerAsync(false, cancellationToken);

            var list = new List<string>();
            BlobContinuationToken token = null;
            do
            {
                var r = await container.ListBlobsSegmentedAsync(prefix, token);
                list.AddRange(r.Results.Select(b => b.Uri.GetLeftPart(UriPartial.Path).Substring(container.Uri.ToString().Length + 1)));
                token = r.ContinuationToken;
            }
            while (token != null);

            return list.ToArray();
        }

        public async Task<string> GetAsync(
            string fileName,
            CancellationToken cancellationToken)
        {
            var blob = await GetBlockBlobAsync(fileName, false, cancellationToken);
            return await blob.DownloadTextAsync(Encoding.UTF8, null, null, null, cancellationToken);
        }

        public async Task SetAsync(
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
