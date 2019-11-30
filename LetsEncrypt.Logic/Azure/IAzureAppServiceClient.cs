using LetsEncrypt.Logic.Azure.Response;
using LetsEncrypt.Logic.Providers.CertificateStores;
using System.Threading;
using System.Threading.Tasks;

namespace LetsEncrypt.Logic.Azure
{
    public interface IAzureAppServiceClient
    {
        Task<AppServiceResponse> GetAppServicePropertiesAsync(string resourceGroupName, string name, CancellationToken cancellationToken);

        Task AssignDomainBindingsAsync(string resourceGroupName, string name, string[] hostnames, ICertificate cert, string location, CancellationToken cancellationToken);

        Task UploadCertificateAsync(AppServiceResponse prop, ICertificate cert, string uploadCertName, string targetResourceGroup, CancellationToken cancellationToken);

        Task DeleteCertificateAsync(string certName, string resourceGroupName, CancellationToken cancellationToken);
        Task<CertificateResponse[]> ListCertificatesAsync(string resourceGroupName, CancellationToken cancellationToken);
    }
}
