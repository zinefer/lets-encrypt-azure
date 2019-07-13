using LetsEncrypt.Logic.Providers.CertificateStores;
using LetsEncrypt.Logic.Providers.ChallengeResponders;
using LetsEncrypt.Logic.Providers.TargetResources;
using System.Threading;
using System.Threading.Tasks;

namespace LetsEncrypt.Logic.Config
{
    public interface IRenewalOptionParser
    {
        Task<IChallengeResponder> ParseChallengeResponderAsync(CertificateRenewalOptions cfg, CancellationToken cancellationToken);

        ICertificateStore ParseCertificateStore(CertificateRenewalOptions cfg);

        ITargetResource ParseTargetResource(CertificateRenewalOptions cfg);
    }
}
