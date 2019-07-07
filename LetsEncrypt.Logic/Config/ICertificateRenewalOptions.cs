using LetsEncrypt.Logic.Providers.CertificateStores;
using LetsEncrypt.Logic.Providers.ChallengeResponders;
using LetsEncrypt.Logic.Providers.TargetResources;
using System.Threading;
using System.Threading.Tasks;

namespace LetsEncrypt.Logic.Config
{
    public interface ICertificateRenewalOptions
    {
        /// <summary>
        /// The set of hostnames for which to issue a certificate.
        /// Note that these will all be issued into a single certificate.
        /// </summary>
        string[] HostNames { get; }

        GenericEntry ChallengeResponder { get; }

        GenericEntry CertificateStore { get; }

        GenericEntry TargetResource { get; }

        Task<IChallengeResponder> ParseChallengeResponderAsync(CancellationToken cancellationToken);

        ICertificateStore ParseCertificateStore();

        ITargetResource ParseTargetResource();
    }
}
