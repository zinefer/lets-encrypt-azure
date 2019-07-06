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
        /// <param name="cfg"></param>
        /// <param name="authenticationContext"></param>
        /// <param name="cancellationToken"></param>
        Task<RenewalResult> RenewCertificateAsync(CertificateConfiguration cfg, AuthenticationContext authenticationContext, CancellationToken cancellationToken);
    }
}
