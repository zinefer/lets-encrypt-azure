namespace LetsEncrypt.Logic.Config.Properties
{
    /// <summary>
    /// Properties related to Azure CDN.
    /// </summary>
    public class CdnProperties
    {
        /// <summary>
        /// The name of the CDN.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The resource group of the CDN.
        /// </summary>
        public string ResourceGroupName { get; set; }

        /// <summary>
        /// The endpoints within the CDN to update.
        /// </summary>
        public string[] Endpoints { get; set; } = new string[0];
    }
}
