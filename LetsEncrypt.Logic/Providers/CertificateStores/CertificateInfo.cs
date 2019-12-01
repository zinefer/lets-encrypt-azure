using Microsoft.Azure.KeyVault.Models;
using System;
using System.Linq;

namespace LetsEncrypt.Logic.Providers.CertificateStores
{
    public class CertificateInfo : ICertificate
    {
        private readonly CertificateBundle _certificateBundle;

        public CertificateInfo(CertificateBundle certificateBundle, ICertificateStore store)
        {
            _certificateBundle = certificateBundle ?? throw new ArgumentNullException(nameof(certificateBundle));
            Store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public DateTime? NotBefore => _certificateBundle.Attributes.NotBefore;

        public DateTime? Expires => _certificateBundle.Attributes.Expires;

        public string Name => _certificateBundle.CertificateIdentifier.Name;

        public string Version => _certificateBundle.CertificateIdentifier.Version;

        public ICertificateStore Store { get; }

        public string[] HostNames => _certificateBundle.Policy.X509CertificateProperties.SubjectAlternativeNames.DnsNames.ToArray();

        public string Thumbprint => ThumbprintHelper.Convert(_certificateBundle.X509Thumbprint);

        public string CertificateVersion => _certificateBundle.CertificateIdentifier.Version;
    }
}
