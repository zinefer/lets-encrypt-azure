using LetsEncrypt.Logic.Providers.CertificateStores;
using System.Threading;
using System.Threading.Tasks;

namespace LetsEncrypt.Logic.Providers.TargetResources
{
    public interface ITargetResource
    {
        string Name { get; }
        Task UpdateAsync(ICertificate cert, CancellationToken cancellationToken);
    }
}
