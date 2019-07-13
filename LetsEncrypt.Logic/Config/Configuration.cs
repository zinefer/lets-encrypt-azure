namespace LetsEncrypt.Logic.Config
{
    /// <summary>
    /// Strongly typed version of the json configuration file.
    /// </summary>
    public class Configuration
    {
        public AcmeOptions Acme { get; set; } = new AcmeOptions();

        public CertificateRenewalOptions[] Certificates { get; set; } = new CertificateRenewalOptions[0];
    }
}
