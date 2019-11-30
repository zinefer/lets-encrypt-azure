using FluentAssertions;
using LetsEncrypt.Logic.Azure;
using LetsEncrypt.Logic.Config;
using LetsEncrypt.Logic.Config.Properties;
using LetsEncrypt.Logic.Storage;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.KeyVault.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Rest;
using Microsoft.Rest.Azure;
using Microsoft.WindowsAzure.Storage;
using Moq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace LetsEncrypt.Tests
{
    public class RenewalOptionParserTests
    {
        [Test]
        public void ParsingChallengeResponderShouldWorkIfCallerHasMsiAccessToStorage()
        {
            var az = new Mock<IAzureHelper>();
            var kv = new Mock<IKeyVaultClient>();
            var factory = new Mock<IStorageFactory>();
            var storage = new Mock<IStorageProvider>();
            // check is used by parser to verify MSI access
            storage.Setup(x => x.ExistsAsync(RenewalOptionParser.FileNameForPermissionCheck, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(false));
            factory.Setup(x => x.FromMsiAsync("example", new StorageProperties().ContainerName, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(storage.Object));

            var log = new Mock<ILogger>();
            IRenewalOptionParser parser = new RenewalOptionParser(
                az.Object,
                kv.Object,
                factory.Object,
                new Mock<IAzureAppServiceClient>().Object,
                new Mock<IAzureCdnClient>().Object,
                log.Object);

            var cfg = TestHelper.LoadConfig("config");
            new Func<Task>(async () => _ = await parser.ParseChallengeResponderAsync(cfg.Certificates[0], CancellationToken.None)).Should().NotThrow();
        }

        [Test]
        public void ParsingChallengeResponderShouldFailIfCallerHasNoMsiAccessToStorageAndFallbacksAreNotAvailable()
        {
            var az = new Mock<IAzureHelper>();
            var kv = new Mock<IKeyVaultClient>();
            var factory = new Mock<IStorageFactory>();
            var storage = new Mock<IStorageProvider>();
            // check is used by parser to verify MSI access
            storage.Setup(x => x.ExistsAsync(RenewalOptionParser.FileNameForPermissionCheck, It.IsAny<CancellationToken>()))
                .Throws(new StorageException(new RequestResult
                {
                    HttpStatusCode = (int)HttpStatusCode.Forbidden
                }, "Access denied, due to missing MSI permissions", null));
            // fallback is keyvault -> secret not found. 
            kv.Setup(x => x.GetSecretWithHttpMessagesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, List<string>>>(), It.IsAny<CancellationToken>()))
                .Throws(new KeyVaultErrorException("denied")
                {
                    Response = new HttpResponseMessageWrapper(new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.NotFound
                    }, "denied")
                });
            factory.Setup(x => x.FromMsiAsync("example", new StorageProperties().ContainerName, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(storage.Object));

            var log = new Mock<ILogger>();
            IRenewalOptionParser parser = new RenewalOptionParser(
                az.Object,
                kv.Object,
                factory.Object,
                new Mock<IAzureAppServiceClient>().Object,
                new Mock<IAzureCdnClient>().Object,
                log.Object);

            var cfg = TestHelper.LoadConfig("config");
            new Func<Task>(async () => _ = await parser.ParseChallengeResponderAsync(cfg.Certificates[0], CancellationToken.None)).Should().Throw<InvalidOperationException>();

            kv.Verify(x => x.GetSecretWithHttpMessagesAsync("https://example.vault.azure.net", new StorageProperties().SecretName, "", null, CancellationToken.None), Times.Once);
        }

        [Test]
        public async Task ParsingChallengeResponderShouldSucceedIfCallerHasNoMsiAccessToStorageButKeyVaultFallbackIsAvailable()
        {
            var az = new Mock<IAzureHelper>();
            var kv = new Mock<IKeyVaultClient>();
            var factory = new Mock<IStorageFactory>();
            var storage = new Mock<IStorageProvider>();
            // check is used by parser to verify MSI access
            storage.Setup(x => x.ExistsAsync(RenewalOptionParser.FileNameForPermissionCheck, It.IsAny<CancellationToken>()))
                .Throws(new StorageException(new RequestResult
                {
                    HttpStatusCode = (int)HttpStatusCode.Forbidden
                }, "Access denied, due to missing MSI permissions", null));
            // fallback is keyvault
            const string connectionString = "this will grant me access";
            kv.Setup(x => x.GetSecretWithHttpMessagesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, List<string>>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new AzureOperationResponse<SecretBundle>
                {
                    Body = new SecretBundle(connectionString)
                }));

            factory.Setup(x => x.FromMsiAsync("example", new StorageProperties().ContainerName, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(storage.Object));
            // fallback
            factory.Setup(x => x.FromConnectionString(connectionString, new StorageProperties().ContainerName))
                .Returns(storage.Object);

            var log = new Mock<ILogger>();
            IRenewalOptionParser parser = new RenewalOptionParser(
                az.Object,
                kv.Object,
                factory.Object,
                new Mock<IAzureAppServiceClient>().Object,
                new Mock<IAzureCdnClient>().Object,
                log.Object);

            var cfg = TestHelper.LoadConfig("config");
            var r = await parser.ParseChallengeResponderAsync(cfg.Certificates[0], CancellationToken.None);

            kv.Verify(x => x.GetSecretWithHttpMessagesAsync("https://example.vault.azure.net", new StorageProperties().SecretName, "", null, CancellationToken.None), Times.Once);
            factory.Verify(x => x.FromConnectionString(connectionString, new StorageProperties().ContainerName), Times.Once);
        }

        [Test]
        public async Task ParsingChallengeResponderShouldSucceedIfCallerHasNoMsiAccessToConnectionStringFallbackIsAvailable()
        {
            var az = new Mock<IAzureHelper>();
            var kv = new Mock<IKeyVaultClient>();
            var factory = new Mock<IStorageFactory>();
            var storage = new Mock<IStorageProvider>();
            // check is used by parser to verify MSI access
            storage.Setup(x => x.ExistsAsync(RenewalOptionParser.FileNameForPermissionCheck, It.IsAny<CancellationToken>()))
                .Throws(new StorageException(new RequestResult
                {
                    HttpStatusCode = (int)HttpStatusCode.Forbidden
                }, "Access denied, due to missing MSI permissions", null));
            // fallback is connectionString
            const string connectionString = "this will grant me access";

            factory.Setup(x => x.FromMsiAsync("example", new StorageProperties().ContainerName, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(storage.Object));
            // fallback
            factory.Setup(x => x.FromConnectionString(connectionString, new StorageProperties().ContainerName))
                .Returns(storage.Object);

            var log = new Mock<ILogger>();
            IRenewalOptionParser parser = new RenewalOptionParser(
                az.Object,
                kv.Object,
                factory.Object,
                new Mock<IAzureAppServiceClient>().Object,
                new Mock<IAzureCdnClient>().Object,
                log.Object);

            var cfg = TestHelper.LoadConfig("config+connectionstring");
            var r = await parser.ParseChallengeResponderAsync(cfg.Certificates[0], CancellationToken.None);

            // keyvault should not be tried if connectionstring is found
            kv.VerifyNoOtherCalls();
            factory.Verify(x => x.FromConnectionString(connectionString, new StorageProperties().ContainerName), Times.Once);
        }

        [Test]
        public void ParsingCertificateStoreShouldWork()
        {
            var az = new Mock<IAzureHelper>();
            var kv = new Mock<IKeyVaultClient>();
            var factory = new Mock<IStorageFactory>();

            var log = new Mock<ILogger>();
            IRenewalOptionParser parser = new RenewalOptionParser(
                az.Object,
                kv.Object,
                factory.Object,
                new Mock<IAzureAppServiceClient>().Object,
                new Mock<IAzureCdnClient>().Object,
                log.Object);

            var cfg = TestHelper.LoadConfig("config");
            var store = parser.ParseCertificateStore(cfg.Certificates[0]);
            store.Type.Should().Be("keyVault");
            store.Name.Should().Be("example");
        }

        [Test]
        public void ParsingCdnResourceShouldWork()
        {
            var az = new Mock<IAzureHelper>();
            var kv = new Mock<IKeyVaultClient>();
            var factory = new Mock<IStorageFactory>();

            var log = new Mock<ILogger>();
            IRenewalOptionParser parser = new RenewalOptionParser(
                az.Object,
                kv.Object,
                factory.Object,
                new Mock<IAzureAppServiceClient>().Object,
                new Mock<IAzureCdnClient>().Object,
                log.Object);

            var cfg = TestHelper.LoadConfig("config");
            var target = parser.ParseTargetResource(cfg.Certificates[0]);
            target.Name.Should().Be("example");
        }
    }
}
