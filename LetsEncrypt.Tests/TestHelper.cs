using LetsEncrypt.Logic.Config;
using System.IO;

namespace LetsEncrypt.Tests
{
    public static class TestHelper
    {
        private const string TestUser = "User.McUserface@example.com";

        public const string DevelopmentStorageConnectionString = "UseDevelopmentStorage=true";

        public const string TestContainerName = "letsencrypt-tests";

        public static IAcmeOptions GetStagingOptions() => new AcmeOptions
        {
            Email = TestUser,
            Staging = true
        };

        public static IAcmeOptions GetProductionOptions() => new AcmeOptions
        {
            Email = TestUser
        };

        public static Configuration LoadConfig(string filename)
        {
            return new ConfigurationProcessor().ValidateAndLoad(File.ReadAllText($"Files/{filename}.json"));
        }
    }
}
