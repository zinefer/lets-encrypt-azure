using System.Threading;
using System.Threading.Tasks;

namespace LetsEncrypt.Logic
{
    public interface IAzureHelper
    {
        string GetSubscriptionId();

        Task<string> GetTenantIdAsync(CancellationToken cancellationToken);
    }
}
