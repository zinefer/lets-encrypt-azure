using Newtonsoft.Json.Linq;

namespace LetsEncrypt.Logic.Config
{
    /// <summary>
    /// Generic base type that allows injecting various properties based on different types of config.
    /// </summary>
    public class GenericEntry
    {
        /// <summary>
        /// The type of the config. Must be well known in advance and is used to determine which properties are expected in <see cref="Properties"/>.
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// Optional. Allows to set a fallback name on the root object that
        /// will be used for all properties that are not set.
        /// Useful if all resources are named the same and it is expected to set e.g. resourceGroupName, keyVaultName, storageAccountName, etc. -> Name will be fallback for all of them.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Additional properties depending on <see cref="Type"/>.
        /// </summary>
        public JObject Properties { get; set; }
    }
}
