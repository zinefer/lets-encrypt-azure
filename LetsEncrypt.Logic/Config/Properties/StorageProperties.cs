namespace LetsEncrypt.Logic.Config.Properties
{
    public class StorageProperties
    {
        public string ContainerName { get; set; } = "$web";

        public string Path { get; set; } = ".well-known/acme-challenge/";

        /// <summary>
        /// Used for MSI authentication.
        /// If not set, defaults to keyvault name of certStore section.
        /// </summary>
        public string AccountName { get; set; }

        /// <summary>
        /// If set, uses the connection string to access the storage account.
        /// Fallback if MSI doesn't work.
        /// </summary>
        public string ConnectionString { get; set; }

        /// <summary>
        /// If set, uses connection string from keyvault.
        /// If not set (and the other auth options didn't work) will fallback to keyvault name of certStore section.
        /// </summary>
        public string KeyVaultName { get; set; }

        /// <summary>
        /// Name of the secret in keyvault that must contain the connection string.
        /// </summary>
        public string SecretName { get; set; } = "Storage";
    }
}
