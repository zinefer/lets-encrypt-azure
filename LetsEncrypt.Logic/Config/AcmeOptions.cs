using Certes.Acme;
using Newtonsoft.Json;
using System;

namespace LetsEncrypt.Logic.Config
{
    /// <summary>
    /// Parameters for a certificate request
    /// </summary>
    public class AcmeOptions : IAcmeOptions
    {
        public bool Staging { get; set; }

        public string Email { get; set; }

        public int RenewXDaysBeforeExpiry { get; set; } = 30;

        [JsonIgnore]
        public Uri CertificateAuthorityUri
            => Staging ? WellKnownServers.LetsEncryptStagingV2 : WellKnownServers.LetsEncryptV2;
    }
}
