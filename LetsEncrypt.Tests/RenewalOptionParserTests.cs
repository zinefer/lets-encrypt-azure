using Azure;
using Azure.Security.KeyVault.Certificates;
using Azure.Security.KeyVault.Secrets;
using FluentAssertions;
using LetsEncrypt.Logic.Azure;
using LetsEncrypt.Logic.Config;
using LetsEncrypt.Logic.Config.Properties;
using LetsEncrypt.Logic.Storage;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System;
using System.Net;
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
            var factory = new Mock<IStorageFactory>();
            var storage = new Mock<IStorageProvider>();
            // check is used by parser to verify MSI access
            storage.Setup(x => x.ExistsAsync(RenewalOptionParser.FileNameForPermissionCheck, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(false));
            factory.Setup(x => x.FromMsiAsync("example", new StorageProperties().ContainerName, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(storage.Object));

            var kvFactory = new Mock<IKeyVaultFactory>();
            var client = new Mock<CertificateClient>();
            kvFactory.Setup(x => x.CreateCertificateClient("example"))
                .Returns(client.Object);
            var log = new Mock<ILoggerFactory>();
            IRenewalOptionParser parser = new RenewalOptionParser(
                az.Object,
                kvFactory.Object,
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
            var factory = new Mock<IStorageFactory>();
            var storage = new Mock<IStorageProvider>();
            // check is used by parser to verify MSI access
            storage.Setup(x => x.ExistsAsync(RenewalOptionParser.FileNameForPermissionCheck, It.IsAny<CancellationToken>()))
                .Throws(new RequestFailedException((int)HttpStatusCode.Forbidden, "Access denied, due to missing MSI permissions"));

            factory.Setup(x => x.FromMsiAsync("example", new StorageProperties().ContainerName, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(storage.Object));

            var log = new Mock<ILoggerFactory>();
            log.Setup(x => x.CreateLogger(It.IsAny<string>()))
                .Returns(new Mock<ILogger>().Object);
            var kvFactory = new Mock<IKeyVaultFactory>();
            var client = new Mock<CertificateClient>();
            var secretClient = new Mock<SecretClient>();
            // fallback is keyvault -> secret not found. 
            secretClient.Setup(x => x.GetSecretAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Throws(new RequestFailedException((int)HttpStatusCode.NotFound, "denied"));
            kvFactory.Setup(x => x.CreateSecretClient("example"))
                .Returns(secretClient.Object);
            kvFactory.Setup(x => x.CreateCertificateClient("example"))
                .Returns(client.Object);

            IRenewalOptionParser parser = new RenewalOptionParser(
                az.Object,
                kvFactory.Object,
                factory.Object,
                new Mock<IAzureAppServiceClient>().Object,
                new Mock<IAzureCdnClient>().Object,
                log.Object);

            var cfg = TestHelper.LoadConfig("config");
            new Func<Task>(async () => _ = await parser.ParseChallengeResponderAsync(cfg.Certificates[0], CancellationToken.None)).Should().Throw<InvalidOperationException>();

            secretClient.Verify(x => x.GetSecretAsync(new StorageProperties().SecretName, null, CancellationToken.None), Times.Once);
        }

        [Test]
        public async Task ParsingChallengeResponderShouldSucceedIfCallerHasNoMsiAccessToStorageButKeyVaultFallbackIsAvailable()
        {
            var az = new Mock<IAzureHelper>();
            var factory = new Mock<IStorageFactory>();
            var storage = new Mock<IStorageProvider>();
            // check is used by parser to verify MSI access
            storage.Setup(x => x.ExistsAsync(RenewalOptionParser.FileNameForPermissionCheck, It.IsAny<CancellationToken>()))
                .Throws(new RequestFailedException((int)HttpStatusCode.Forbidden, "Access denied, due to missing MSI permissions"));

            factory.Setup(x => x.FromMsiAsync("example", new StorageProperties().ContainerName, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(storage.Object));

            var log = new Mock<ILoggerFactory>();
            log.Setup(x => x.CreateLogger(It.IsAny<string>()))
                .Returns(new Mock<ILogger>().Object);
            var kvFactory = new Mock<IKeyVaultFactory>();
            var client = new Mock<CertificateClient>();
            var secretClient = new Mock<SecretClient>();
            // fallback is keyvault
            const string connectionString = "this will grant me access";
            var response = new Mock<Response<KeyVaultSecret>>();
            response.SetupGet(x => x.Value)
                .Returns(new KeyVaultSecret(new StorageProperties().SecretName, connectionString));
            secretClient.Setup(x => x.GetSecretAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(response.Object);
            kvFactory.Setup(x => x.CreateSecretClient("example"))
                .Returns(secretClient.Object);
            kvFactory.Setup(x => x.CreateCertificateClient("example"))
                .Returns(client.Object);

            // fallback
            factory.Setup(x => x.FromConnectionString(connectionString, new StorageProperties().ContainerName))
                .Returns(storage.Object);
            IRenewalOptionParser parser = new RenewalOptionParser(
                az.Object,
                kvFactory.Object,
                factory.Object,
                new Mock<IAzureAppServiceClient>().Object,
                new Mock<IAzureCdnClient>().Object,
                log.Object);

            var cfg = TestHelper.LoadConfig("config");
            var r = await parser.ParseChallengeResponderAsync(cfg.Certificates[0], CancellationToken.None);

            secretClient.Verify(x => x.GetSecretAsync(new StorageProperties().SecretName, null, CancellationToken.None), Times.Once);
            factory.Verify(x => x.FromConnectionString(connectionString, new StorageProperties().ContainerName), Times.Once);
        }

        [Test]
        public async Task ParsingChallengeResponderShouldSucceedIfCallerHasNoMsiAccessToConnectionStringFallbackIsAvailable()
        {
            var az = new Mock<IAzureHelper>();
            var factory = new Mock<IStorageFactory>();
            var storage = new Mock<IStorageProvider>();
            // check is used by parser to verify MSI access
            storage.Setup(x => x.ExistsAsync(RenewalOptionParser.FileNameForPermissionCheck, It.IsAny<CancellationToken>()))
                .Throws(new RequestFailedException((int)HttpStatusCode.Forbidden, "Access denied, due to missing MSI permissions"));
            // fallback is connectionString
            const string connectionString = "this will grant me access";

            factory.Setup(x => x.FromMsiAsync("example", new StorageProperties().ContainerName, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(storage.Object));
            // fallback
            factory.Setup(x => x.FromConnectionString(connectionString, new StorageProperties().ContainerName))
                .Returns(storage.Object);

            var log = new Mock<ILoggerFactory>();
            log.Setup(x => x.CreateLogger(It.IsAny<string>()))
                .Returns(new Mock<ILogger>().Object);
            var kvFactory = new Mock<IKeyVaultFactory>();
            var client = new Mock<CertificateClient>();
            var secretClient = new Mock<SecretClient>();
            kvFactory.Setup(x => x.CreateSecretClient("example"))
                .Returns(secretClient.Object);
            kvFactory.Setup(x => x.CreateCertificateClient("example"))
                .Returns(client.Object);
            IRenewalOptionParser parser = new RenewalOptionParser(
                az.Object,
                kvFactory.Object,
                factory.Object,
                new Mock<IAzureAppServiceClient>().Object,
                new Mock<IAzureCdnClient>().Object,
                log.Object);

            var cfg = TestHelper.LoadConfig("config+connectionstring");
            var r = await parser.ParseChallengeResponderAsync(cfg.Certificates[0], CancellationToken.None);

            // keyvault should not be tried if connectionstring is found
            secretClient.VerifyNoOtherCalls();
            factory.Verify(x => x.FromConnectionString(connectionString, new StorageProperties().ContainerName), Times.Once);
        }

        [Test]
        public void ParsingCertificateStoreShouldWork()
        {
            var az = new Mock<IAzureHelper>();
            var factory = new Mock<IStorageFactory>();

            var log = new Mock<ILoggerFactory>();
            var kvFactory = new Mock<IKeyVaultFactory>();
            var client = new Mock<CertificateClient>();
            kvFactory.Setup(x => x.CreateCertificateClient("example"))
                .Returns(client.Object);
            IRenewalOptionParser parser = new RenewalOptionParser(
                az.Object,
                kvFactory.Object,
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
            var factory = new Mock<IStorageFactory>();

            var log = new Mock<ILoggerFactory>();
            var kvFactory = new Mock<IKeyVaultFactory>();
            var client = new Mock<CertificateClient>();
            kvFactory.Setup(x => x.CreateCertificateClient("example"))
                .Returns(client.Object);
            IRenewalOptionParser parser = new RenewalOptionParser(
                az.Object,
                kvFactory.Object,
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
