using Certes.Acme;
using LetsEncrypt.Logic.Config;
using System.Threading;
using System.Threading.Tasks;

namespace LetsEncrypt.Logic.Acme
{
    public interface ICertificateBuilder
    {
        /// <summary>
        /// Helper to build a certificate from an already processed & validated order.
        /// </summary>
        /// <param name="order"></param>
        /// <param name="cfg"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<(byte[] pfxBytes, string password)> BuildCertificateAsync(IOrderContext order, CertificateRenewalOptions cfg, CancellationToken cancellationToken);
    }
}
