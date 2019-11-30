using LetsEncrypt.Logic.Azure;
using LetsEncrypt.Logic.Azure.Response;
using LetsEncrypt.Logic.Providers.CertificateStores;
using LetsEncrypt.Logic.Providers.TargetResources;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System.Threading;
using System.Threading.Tasks;

namespace LetsEncrypt.Tests
{
    public class AppServiceTargetResourceTests
    {
        [Test]
        public async Task VerifiyTargetCalls()
        {
            const string tenant = "tenantId";
            const string subscriptionId = "subscriptionId";
            const string resourceGroupName = "rg";
            const string name = "name";
            var az = new Mock<IAzureHelper>();
            az.Setup(x => x.GetTenantIdAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(tenant));
            az.Setup(x => x.GetSubscriptionId())
                .Returns(subscriptionId);

            var client = new Mock<IAzureAppServiceClient>();
            var asp = new AppServiceResponse
            {
                Location = "westeurope",
                ServerFarmId = $"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/",
                Hostnames = new[] { "example.com", "www.example.com" }
            };
            client.Setup(x => x.GetAppServicePropertiesAsync(resourceGroupName, name, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(asp));
            var store = new Mock<ICertificateStore>();
            var resource = new AppServiceTargetResoure(
                client.Object,
                store.Object,
                resourceGroupName,
                name,
                new Mock<ILogger<AppServiceTargetResoure>>().Object);

            store.SetupGet(x => x.Type)
                .Returns("keyVault");
            var cert = new Mock<ICertificate>();
            cert.SetupGet(x => x.HostNames)
                .Returns(new[] { "www.example.com", "example.com" });
            cert.SetupGet(x => x.Thumbprint)
                .Returns("THUMBPRINT");
            cert.SetupGet(x => x.Store)
                .Returns(store.Object);
            await resource.UpdateAsync(cert.Object, CancellationToken.None);

            client.Verify(x => x.UploadCertificateAsync(asp, cert.Object, "www.example.com-THUMBPRINT", resourceGroupName, It.IsAny<CancellationToken>()), Times.Once);
            client.Verify(x => x.GetAppServicePropertiesAsync(resourceGroupName, "name", It.IsAny<CancellationToken>()), Times.Once);
            client.Verify(x => x.AssignDomainBindingsAsync(resourceGroupName, "name", new[] { "www.example.com", "example.com" }, cert.Object, "westeurope", It.IsAny<CancellationToken>()), Times.Once);
            client.Verify(x => x.ListCertificatesAsync(resourceGroupName, It.IsAny<CancellationToken>()), Times.Once);

            client.VerifyNoOtherCalls();
        }

        [Test]
        public async Task OldBindingsShouldBeDeletedOnSuccess()
        {
            const string tenant = "tenantId";
            const string subscriptionId = "subscriptionId";
            const string resourceGroupName = "rg";
            const string name = "name";
            var az = new Mock<IAzureHelper>();
            az.Setup(x => x.GetTenantIdAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(tenant));
            az.Setup(x => x.GetSubscriptionId())
                .Returns(subscriptionId);

            var client = new Mock<IAzureAppServiceClient>();
            var asp = new AppServiceResponse
            {
                Location = "westeurope",
                ServerFarmId = $"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/",
                Hostnames = new[] { "example.com", "www.example.com" }
            };
            client.Setup(x => x.GetAppServicePropertiesAsync(resourceGroupName, name, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(asp));

            client.Setup(x => x.ListCertificatesAsync(resourceGroupName, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new[]
                {
                    new CertificateResponse
                    {
                        Name = "www-example-com-1337",
                        Thumbprint = "1337",
                        HostNames = new[] { "www.example.com", "example.com" }
                    },
                    new CertificateResponse
                    {
                        Name = "not-relevant",
                        Thumbprint = "1338",
                        HostNames = new[] { "anotherdomain.com" }
                    }
                }));
            var store = new Mock<ICertificateStore>();
            var resource = new AppServiceTargetResoure(
                client.Object,
                store.Object,
                resourceGroupName,
                name,
                new Mock<ILogger<AppServiceTargetResoure>>().Object);

            store.SetupGet(x => x.Type)
                .Returns("keyVault");
            store.Setup(x => x.GetCertificateThumbprintsAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new[]
                {
                    "1337",
                    "1338"
                }));
            var cert = new Mock<ICertificate>();
            cert.SetupGet(x => x.HostNames)
                .Returns(new[] { "www.example.com", "example.com" });
            cert.SetupGet(x => x.Thumbprint)
                .Returns("THUMBPRINT");
            cert.SetupGet(x => x.Store)
                .Returns(store.Object);
            await resource.UpdateAsync(cert.Object, CancellationToken.None);

            client.Verify(x => x.UploadCertificateAsync(asp, cert.Object, "www.example.com-THUMBPRINT", resourceGroupName, It.IsAny<CancellationToken>()), Times.Once);
            client.Verify(x => x.GetAppServicePropertiesAsync(resourceGroupName, "name", It.IsAny<CancellationToken>()), Times.Once);
            client.Verify(x => x.AssignDomainBindingsAsync(resourceGroupName, "name", new[] { "www.example.com", "example.com" }, cert.Object, "westeurope", It.IsAny<CancellationToken>()), Times.Once);
            client.Verify(x => x.ListCertificatesAsync(resourceGroupName, It.IsAny<CancellationToken>()), Times.Once);
            client.Verify(x => x.DeleteCertificateAsync("www-example-com-1337", resourceGroupName, It.IsAny<CancellationToken>()), Times.Once);

            client.VerifyNoOtherCalls();
        }

        // regression https://github.com/MarcStan/lets-encrypt-azure/issues/7
        [Test]
        public async Task ShouldNotDeleteUnrelatedCertificateBindingsOnSuccess()
        {
            const string tenant = "tenantId";
            const string subscriptionId = "subscriptionId";
            const string resourceGroupName = "rg";
            const string name = "name";
            var az = new Mock<IAzureHelper>();
            az.Setup(x => x.GetTenantIdAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(tenant));
            az.Setup(x => x.GetSubscriptionId())
                .Returns(subscriptionId);

            var client = new Mock<IAzureAppServiceClient>();
            var asp = new AppServiceResponse
            {
                Location = "westeurope",
                ServerFarmId = $"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/",
                Hostnames = new[] { "abc.mydomain.com" }
            };
            client.Setup(x => x.GetAppServicePropertiesAsync(resourceGroupName, name, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(asp));

            client.Setup(x => x.ListCertificatesAsync(resourceGroupName, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new[]
                {
                    // should be deleted; old of current domain
                    new CertificateResponse
                    {
                        Name = "abc-mydomain-com-old-THUMB",
                        Thumbprint = "old-THUMB",
                        HostNames = new[] { "abc.mydomain.com" }
                    },
                    // should be kept; thumbprint matches but has different domains assigned as well
                    new CertificateResponse
                    {
                        Name = "abc-mydomain-com-old-THUMB",
                        Thumbprint = "old-THUMB",
                        HostNames = new[] { "abc.mydomain.com", "foo.mydomain.com" }
                    },
                    // should be kept; totally different domain
                    new CertificateResponse
                    {
                        Name = "mydomain-com-1337",
                        Thumbprint = "1337",
                        HostNames = new[] { "*.mydomain.com", "mydomain.com" }
                    },
                    // should be kept; totally different domain
                    new CertificateResponse
                    {
                        Name = "mydomain-com-42",
                        Thumbprint = "42",
                        HostNames = new[] { "www.mydomain.com", "mydomain.com" }
                    }
                }));
            var store = new Mock<ICertificateStore>();
            store.Setup(x => x.GetCertificateThumbprintsAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new[]
                {
                    "old-THUMB",
                    // in unlikely event that thumbprint match those of other domains we still also check domain name
                    "42"
                }));
            var resource = new AppServiceTargetResoure(
                client.Object,
                store.Object,
                resourceGroupName,
                name,
                new Mock<ILogger<AppServiceTargetResoure>>().Object);

            store.SetupGet(x => x.Type)
                .Returns("keyVault");
            var cert = new Mock<ICertificate>();
            cert.SetupGet(x => x.HostNames)
                .Returns(new[] { "abc.mydomain.com" });
            cert.SetupGet(x => x.Thumbprint)
                .Returns("THUMBPRINT");
            cert.SetupGet(x => x.Store)
                .Returns(store.Object);
            await resource.UpdateAsync(cert.Object, CancellationToken.None);

            client.Verify(x => x.UploadCertificateAsync(asp, cert.Object, "abc.mydomain.com-THUMBPRINT", resourceGroupName, It.IsAny<CancellationToken>()), Times.Once);
            client.Verify(x => x.GetAppServicePropertiesAsync(resourceGroupName, "name", It.IsAny<CancellationToken>()), Times.Once);
            client.Verify(x => x.AssignDomainBindingsAsync(resourceGroupName, "name", new[] { "abc.mydomain.com" }, cert.Object, "westeurope", It.IsAny<CancellationToken>()), Times.Once);
            client.Verify(x => x.ListCertificatesAsync(resourceGroupName, It.IsAny<CancellationToken>()), Times.Once);
            client.Verify(x => x.DeleteCertificateAsync("abc-mydomain-com-old-THUMB", resourceGroupName, It.IsAny<CancellationToken>()), Times.Once);

            client.VerifyNoOtherCalls();
        }
    }
}
