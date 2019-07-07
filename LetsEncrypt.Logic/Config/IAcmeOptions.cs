using Certes.Acme;
using System;

namespace LetsEncrypt.Logic.Config
{
    public interface IAcmeOptions
    {
        /// <summary>
        /// Use acme staging environment (does not generate real certs but allows unlimited requests for testing).
        /// </summary>
        bool Staging { get; }

        /// <summary>
        /// The user email for whom to generate certificates.
        /// Will receive emails from LetsEncrypt if certificates get close to expiry.
        /// </summary>
        string Email { get; }

        /// <summary>
        /// Days before certificate expire when the renewal should occur.
        /// Defaults to 30 days before expiry.
        /// </summary>
        int RenewXDaysBeforeExpiry { get; }

        /// <summary>
        /// Uri of LetsEncrypt authority. See <see cref="WellKnownServers"/>.
        /// Will be set based on value of <see cref="Staging"/>
        /// </summary>
        Uri CertificateAuthorityUri { get; }
    }
}
