namespace LetsEncrypt.Logic.Config
{
    public class Configuration
    {
        public AcmeOptions Acme { get; set; } = new AcmeOptions();

        public CertificateRenewalOptions[] Certificates { get; set; } = new CertificateRenewalOptions[0];
    }
}
