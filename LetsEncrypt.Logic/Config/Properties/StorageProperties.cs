namespace LetsEncrypt.Logic.Config.Properties
{
    /// <summary>
    /// Properties related to Let's encrypt challenge verification via storage account.
    /// </summary>
    public class StorageProperties
    {
        /// <summary>
        /// The container where to store the challenge files.
        /// Assumes the public $web container if not specified.
        /// </summary>
        public string ContainerName { get; set; } = "$web";

        /// <summary>
        /// The path within the container where to store files.
        /// Assumes the default let's encrypt path (.well-known/acme-challenge/) if not set.
        /// </summary>
        public string Path { get; set; } = ".well-known/acme-challenge/";

        // TODO: all properties below are for auth and mutually exclusive. Perhaps a subtype auth with same type system as rest would be better, but then config gets one more nested layer..

        /// <summary>
        /// Used for MSI authentication (preferred access method).
        /// If not set, defaults to keyvault name of certStore section.
        /// </summary>
        public string AccountName { get; set; }

        /// <summary>
        /// If set, uses the connection string to access the storage account.
        /// Fallback only if MSI doesn't work.
        /// </summary>
        public string ConnectionString { get; set; }

        /// <summary>
        /// If set, uses connection string from keyvault.
        /// If not set (and the other auth options didn't work) will fallback to keyvault name of certStore section.
        /// </summary>
        public string KeyVaultName { get; set; }

        /// <summary>
        /// Name of the secret in keyvault that must contain the connection string.
        /// Used in combination with <see cref="KeyVaultName"/>
        /// </summary>
        public string SecretName { get; set; } = "Storage";
    }
}
