namespace LetsEncrypt.Logic.Config
{
    public class CertificateConfiguration
    {
        /// <summary>
        /// The connection string of the azure blob storage for which a certificate will be issued.
        /// Requires the '$web' container to be publicly accessible via http.
        /// </summary>
        public string StorageAccountConnectionString { get; set; }

        /// <summary>
        /// The set of hostnames for which to issue certificates.
        /// Note that these will all be issued into a single certificate.
        /// </summary>
        public string[] HostNames { get; set; }

        public string KeyVaultSubscriptionId { get; set; }

        public string KeyVaultResourceGroupName { get; set; }

        /// <summary>
        /// The keyvault in which to persist the new certificate.
        /// Requires Create + Update permissions for the MSI of the function app.
        /// </summary>
        public string KeyVaultName { get; set; }

        /// <summary>
        /// Optional. The name of the certificate in the keyvault.
        /// If not set, the first hostname is taken and all "." are replaced with "-".
        /// If set, beware that keyvault only allows alphanumeric characters and dashes.
        /// </summary>
        public string CertificateName { get; set; }

        /// <summary>
        /// The cdn details which need to be updated once the certificate is renewed.
        /// </summary>
        public CdnDetails[] CdnDetails { get; set; }
    }
}
