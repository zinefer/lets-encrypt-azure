using System.Threading;
using System.Threading.Tasks;

namespace LetsEncrypt.Logic.Providers.CertificateStores
{
    public interface ICertificateStore
    {
        string Name { get; }

        string Type { get; }

        /// <summary>
        /// Gets the azure resource id for this store.
        /// </summary>
        string ResourceId { get; }

        Task<ICertificate> GetCertificateAsync(CancellationToken cancellationToken);

        Task<ICertificate> UploadAsync(byte[] pfxBytes, string password, string[] hostNames, CancellationToken cancellationToken);
    }
}
