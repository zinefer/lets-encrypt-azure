using Certes.Acme;
using System;

namespace LetsEncrypt.Logic.Config
{
    /// <summary>
    /// Parameters for a certificate request
    /// </summary>
    public class AcmeOptions : IAcmeOptions
    {
        public AcmeOptions(bool staging)
        {
            Staging = staging;
        }

        public AcmeOptions(bool staging, string email)
        {
            Staging = staging;
            Email = email ?? throw new ArgumentNullException(nameof(email));
        }

        public bool Staging { get; }

        public string Email { get; set; }

        public Uri CertificateAuthorityUri
            => Staging ? WellKnownServers.LetsEncryptStagingV2 : WellKnownServers.LetsEncryptV2;
    }
}
