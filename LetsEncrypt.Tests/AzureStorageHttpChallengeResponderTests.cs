using Certes.Acme;
using FluentAssertions;
using LetsEncrypt.Logic;
using LetsEncrypt.Logic.Config;
using LetsEncrypt.Logic.Storage;
using Microsoft.Azure.KeyVault;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LetsEncrypt.Tests
{
    public class AzureStorageHttpChallengeResponderTests
    {
        [Test]
        public async Task LoadingConfigWithCustomStoragePathShouldUseIt()
        {
            IConfigurationProcessor processor = new ConfigurationProcessor();
            var content = File.ReadAllText("Files/config+custompath.json");
            var cfg = processor.ValidateAndLoad(content);
            var cert = cfg.Certificates[0];
            cert.HostNames.Should().BeEquivalentTo(new[]
            {
                "example.com", "www.example.com"
            });
            cert.ChallengeResponder.Should().NotBeNull();

            // fake grant MSI access
            var factory = new Mock<IStorageFactory>();
            var storage = new Mock<IStorageProvider>();
            storage.Setup(x => x.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(true));
            factory.Setup(x => x.FromMsiAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(storage.Object));

            var parser = new RenewalOptionParser(
                new Mock<IAzureHelper>().Object,
                new Mock<IKeyVaultClient>().Object,
                factory.Object,
                new Mock<ILogger>().Object);
            var responder = await parser.ParseChallengeResponderAsync(cert, CancellationToken.None);

            var ctx = new Mock<IChallengeContext>();
            // Certes .Http() extension method internall filters for this type
            ctx.SetupGet(x => x.Type)
                .Returns("http-01");

            ctx.SetupGet(x => x.Token)
                .Returns("fileNAME");
            ctx.SetupGet(x => x.KeyAuthz)
                .Returns("$content");

            var auth = new Mock<IAuthorizationContext>();
            auth.Setup(x => x.Challenges())
                .Returns(Task.FromResult(new[] { ctx.Object }.AsEnumerable()));
            var order = new Mock<IOrderContext>();
            order.Setup(x => x.Authorizations())
                .Returns(Task.FromResult(new[] { auth.Object }.AsEnumerable()));
            _ = await responder.InitiateChallengesAsync(order.Object, CancellationToken.None);

            const string pathPrefix = "not/well-known/";
            storage.Verify(x => x.SetAsync(pathPrefix + "fileNAME", "$content", It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
