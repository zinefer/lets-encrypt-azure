using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace LetsEncrypt.Logic.Azure
{
    public interface IAzureHelper
    {
        string GetSubscriptionId();

        Task<string> GetTenantIdAsync(CancellationToken cancellationToken);

        Task<HttpClient> GetAuthenticatedARMClientAsync(CancellationToken cancellationToken);
    }
}
