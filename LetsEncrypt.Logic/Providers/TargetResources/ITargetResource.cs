using LetsEncrypt.Logic.Providers.CertificateStores;
using System.Threading;
using System.Threading.Tasks;

namespace LetsEncrypt.Logic.Providers.TargetResources
{
    public interface ITargetResource
    {
        /// <summary>
        /// Resource name in azure.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// The type of the resource.
        /// </summary>
        string Type { get; }

        Task UpdateAsync(ICertificate cert, CancellationToken cancellationToken);
    }
}
