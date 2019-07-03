using LetsEncrypt.Logic.Config;
using System.Threading;
using System.Threading.Tasks;

namespace LetsEncrypt.Logic.Authentication
{
    public interface IAuthenticationService
    {
        /// <summary>
        /// Registers a new account or logs into an existing account.
        /// </summary>
        /// <returns></returns>
        Task<AuthenticationContext> AuthenticateAsync(IAcmeOptions options, CancellationToken cancellationToken);
    }
}
