using Certes;
using LetsEncrypt.Logic.Config;
using System;

namespace LetsEncrypt.Logic.Authentication
{
    public class AuthenticationContext
    {
        public AuthenticationContext(IAcmeContext acme, IAcmeOptions options)
        {
            AcmeContext = acme ?? throw new ArgumentNullException(nameof(acme));
            Options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public IAcmeContext AcmeContext { get; }

        public IAcmeOptions Options { get; }
    }
}
