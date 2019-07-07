namespace LetsEncrypt.Logic.Config.Properties
{
    public class StorageProperties
    {
        public string ContainerName { get; set; } = "$web";

        public string Path { get; set; } = ".well-known/acme-challenge/";

        public string ConnectionString { get; set; }

        public string KeyVaultName { get; set; }

        public string SecretName { get; set; } = "Storage";
    }
}
