using LetsEncrypt.Logic.Azure.Response;
using LetsEncrypt.Logic.Providers.CertificateStores;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace LetsEncrypt.Logic.Azure
{
    public interface IAzureCdnClient
    {
        Task<CdnResponse[]> ListEndpointsAsync(string resourceGroupName, string name, CancellationToken cancellationToken);

        Task<HttpResponseMessage[]> UpdateEndpointsAsync(string resourceGroupName, string name, CdnResponse[] endpoints, ICertificate cert, CancellationToken cancellationToken);

        Task<CdnCustomDomainResponse[]> GetCustomDomainDetailsAsync(string resourceGroupName, string name, CdnResponse endpoint, CancellationToken cancellationToken);
    }
}
