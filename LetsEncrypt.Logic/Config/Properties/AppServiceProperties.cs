namespace LetsEncrypt.Logic.Config.Properties
{
    /// <summary>
    /// Properties related to Azure App Service.
    /// </summary>
    public class AppServiceProperties
    {
        /// <summary>
        /// The name of the App Service.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The resource group of the App Service.
        /// </summary>
        public string ResourceGroupName { get; set; }
    }
}
