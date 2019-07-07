using Microsoft.Azure.KeyVault.Models;
using System;
using System.Linq;

namespace LetsEncrypt.Logic.Providers.CertificateStores
{
    public class CertificateInfo : ICertificate
    {
        private readonly CertificateBundle _certificateBundle;

        public CertificateInfo(CertificateBundle certificateBundle, string keyVaultName)
        {
            _certificateBundle = certificateBundle ?? throw new ArgumentNullException(nameof(certificateBundle));
            Origin = keyVaultName ?? throw new ArgumentNullException(nameof(keyVaultName));
        }

        public DateTime? NotBefore => _certificateBundle.Attributes.NotBefore;

        public DateTime? Expires => _certificateBundle.Attributes.Expires;

        public string Name => _certificateBundle.CertificateIdentifier.Name;

        public string Version => _certificateBundle.CertificateIdentifier.Version;

        public string Origin { get; }

        public string[] HostNames => _certificateBundle.Policy.X509CertificateProperties.SubjectAlternativeNames.DnsNames.ToArray();
    }
}
