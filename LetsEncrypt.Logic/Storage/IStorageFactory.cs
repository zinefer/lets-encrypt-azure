using System.Threading;
using System.Threading.Tasks;

namespace LetsEncrypt.Logic.Storage
{
    public interface IStorageFactory
    {
        /// <summary>
        /// Given a connection string returns a provider that can access the specific container using the connection string.
        /// Note that no permissions are checked while creating the provider.
        /// </summary>
        /// <param name="connectionString"></param>
        /// <param name="containerName"></param>
        /// <returns></returns>
        IStorageProvider FromConnectionString(string connectionString, string containerName);

        /// <summary>
        /// Given a storage account name and container returns a provider that can access the specific container using MSI.
        /// Note that no permissions are checked while creating the provider.
        /// Caller must be "Azure Blob Storage Contributor" on the container.
        /// </summary>
        /// <param name="connectionString"></param>
        /// <param name="containerName"></param>
        /// <returns></returns>
        Task<IStorageProvider> FromMsiAsync(string accountName, string containerName, CancellationToken cancellationToken);
    }
}
