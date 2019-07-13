namespace LetsEncrypt.Logic.Config.Properties
{
    /// <summary>
    /// Properties related to storing the certificate in a keyvault.
    /// </summary>
    public class KeyVaultProperties
    {
        /// <summary>
        /// Name of the keyvault where the certificate should be stored.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The name of the certificate in the keyvault.
        /// Limited to alphanumerical and dashes.
        /// Recommended to use domain name with dots replaced by dashes. E.g. example-com
        /// </summary>
        public string CertificateName { get; set; }
    }
}
