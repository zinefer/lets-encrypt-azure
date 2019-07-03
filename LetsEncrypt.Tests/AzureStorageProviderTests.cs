using FluentAssertions;
using LetsEncrypt.Logic.Storage;
using NUnit.Framework;

namespace LetsEncrypt.Tests
{
    public class AzureStorageProviderTests
    {
        [Test]
        public void EncodingShouldMakeInputUriCompatible()
        {
            IStorageProvider storageProvider = new AzureBlobStorageProvider(TestHelper.DevelopmentStorageConnectionString, TestHelper.TestContainerName);

            storageProvider.Escape("user@example.com").Should().Be("user%40example.com");
        }
    }
}
