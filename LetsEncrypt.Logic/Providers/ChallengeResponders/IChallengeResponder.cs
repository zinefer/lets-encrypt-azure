using Certes.Acme;
using System.Threading;
using System.Threading.Tasks;

namespace LetsEncrypt.Logic.Providers.ChallengeResponders
{
    public interface IChallengeResponder
    {
        /// <summary>
        /// Allows selection and preparation of a challenge.
        /// </summary>
        /// <returns></returns>
        Task<IChallengeContext[]> InitiateChallengesAsync(IOrderContext order, CancellationToken cancellationToken);

        /// <summary>
        /// Called when the challenge has been completed to allow cleanup of any intermediate challenge resources.
        /// </summary>
        /// <returns></returns>
        Task CleanupAsync(IChallengeContext[] challengeContexts, CancellationToken cancellationToken);
    }
}
