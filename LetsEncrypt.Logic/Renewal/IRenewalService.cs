using LetsEncrypt.Logic.Authentication;
using LetsEncrypt.Logic.Config;
using System.Threading;
using System.Threading.Tasks;

namespace LetsEncrypt.Logic.Renewal
{
    public interface IRenewalService
    {
        /// <summary>
        /// When called will generate ONE new certificate for the given set of hostnames.
        /// </summary>
        /// <param name="certificateConfiguration"></param>
        /// <param name="authenticationContext"></param>
        /// <param name="cancellationToken"></param>
        /// <returns>The pfx bytes of the certificate.</returns>
        Task<byte[]> RenewCertificateAsync(CertificateConfiguration certificateConfiguration, AuthenticationContext authenticationContext, CancellationToken cancellationToken);
    }
}
