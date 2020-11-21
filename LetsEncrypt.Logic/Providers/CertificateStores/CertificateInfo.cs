using Azure.Security.KeyVault.Certificates;
using System;
using System.Linq;

namespace LetsEncrypt.Logic.Providers.CertificateStores
{
    public class CertificateInfo : ICertificate
    {
        private readonly KeyVaultCertificateWithPolicy _certificate;

        public CertificateInfo(KeyVaultCertificateWithPolicy certificate, ICertificateStore store)
        {
            _certificate = certificate ?? throw new ArgumentNullException(nameof(certificate));
            Store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public DateTimeOffset? NotBefore => _certificate.Properties.NotBefore;

        public DateTimeOffset? Expires => _certificate.Properties.ExpiresOn;

        public string Name => _certificate.Name;

        public ICertificateStore Store { get; }

        public string[] HostNames => _certificate.Policy.SubjectAlternativeNames.DnsNames.ToArray();

        public string Thumbprint => ThumbprintHelper.Convert(_certificate.Properties.X509Thumbprint);

        public string Version => _certificate.Properties.Version;
    }
}
