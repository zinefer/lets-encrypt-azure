using Certes;
using Certes.Acme;
using Certes.Acme.Resource;
using FluentAssertions;
using LetsEncrypt.Logic;
using LetsEncrypt.Logic.Acme;
using LetsEncrypt.Logic.Authentication;
using LetsEncrypt.Logic.Config;
using LetsEncrypt.Logic.Providers.CertificateStores;
using LetsEncrypt.Logic.Providers.ChallengeResponders;
using LetsEncrypt.Logic.Providers.TargetResources;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LetsEncrypt.Tests
{
    public class RenewalServiceTests
    {
        [Test]
        public async Task NewCertificateShouldUpdateTargetResource()
        {
            var config = TestHelper.LoadConfig("config");
            var auth = new Mock<IAuthenticationService>();
            var certBuilder = new Mock<ICertificateBuilder>();
            var ctx = new Mock<IAcmeContext>();
            var orderContext = new Mock<IOrderContext>();
            var authContext = new AuthenticationContext(ctx.Object, config.Acme);
            var parser = new Mock<IRenewalOptionParser>();
            var log = new Mock<ILogger<RenewalService>>();
            IRenewalService service = new RenewalService(auth.Object, parser.Object, certBuilder.Object, log.Object);
            var certStore = new Mock<ICertificateStore>();
            var challengeResponder = new Mock<IChallengeResponder>();
            var challengeContext = new Mock<IChallengeContext>();
            var challenge = new Challenge
            {
                Status = ChallengeStatus.Valid
            };
            var fakeChain = new CertificateChain("fakeChain");
            var pfxBytes = new byte[] { 0xD, 0xE, 0xA, 0xD, 0xB, 0xE, 0xE, 0xF };
            const string fakePassword = "guaranteed to be chosen randomly";
            var targetResource = new Mock<ITargetResource>();
            var cert = new Mock<ICertificate>();

            // check if outdated (yes, not found)
            parser.Setup(x => x.ParseCertificateStore(config.Certificates[0]))
                .Returns(certStore.Object);

            // let's encrypt challenge
            auth.Setup(x => x.AuthenticateAsync(config.Acme, CancellationToken.None))
                .Returns(Task.FromResult(authContext));
            ctx.Setup(x => x.NewOrder(config.Certificates[0].HostNames, null, null))
                .Returns(Task.FromResult(orderContext.Object));
            parser.Setup(x => x.ParseChallengeResponderAsync(config.Certificates[0], CancellationToken.None))
                .Returns(Task.FromResult(challengeResponder.Object));
            challengeResponder.Setup(x => x.InitiateChallengesAsync(orderContext.Object, CancellationToken.None))
                .Returns(Task.FromResult(new[] { challengeContext.Object }));
            challengeContext.Setup(x => x.Resource())
                .Returns(Task.FromResult(challenge));

            // save cert
            certBuilder.Setup(x => x.BuildCertificateAsync(orderContext.Object, config.Certificates[0], CancellationToken.None))
                .Returns(Task.FromResult((pfxBytes, fakePassword)));
            certStore.Setup(x => x.UploadAsync(pfxBytes, fakePassword, config.Certificates[0].HostNames, CancellationToken.None))
                .Returns(Task.FromResult(cert.Object));

            // update azure resource
            parser.Setup(x => x.ParseTargetResource(config.Certificates[0]))
                .Returns(targetResource.Object);

            // actual run - must run through all steps as no existing cert is found
            var r = await service.RenewCertificateAsync(config.Acme, config.Certificates[0], CancellationToken.None);
            r.Should().Be(RenewalResult.Success);

            certStore.Verify(x => x.UploadAsync(pfxBytes, fakePassword, config.Certificates[0].HostNames, CancellationToken.None));
            targetResource.Verify(x => x.UpdateAsync(cert.Object, CancellationToken.None), Times.Once);
        }

        [Test]
        public async Task ExpiredCertificateShouldUpdateTargetResource()
        {
            var config = TestHelper.LoadConfig("config");
            var auth = new Mock<IAuthenticationService>();
            var certBuilder = new Mock<ICertificateBuilder>();
            var ctx = new Mock<IAcmeContext>();
            var orderContext = new Mock<IOrderContext>();
            var authContext = new AuthenticationContext(ctx.Object, config.Acme);
            var parser = new Mock<IRenewalOptionParser>();
            var log = new Mock<ILogger<RenewalService>>();
            IRenewalService service = new RenewalService(auth.Object, parser.Object, certBuilder.Object, log.Object);
            var certStore = new Mock<ICertificateStore>();
            var challengeResponder = new Mock<IChallengeResponder>();
            var challengeContext = new Mock<IChallengeContext>();
            var challenge = new Challenge
            {
                Status = ChallengeStatus.Valid
            };
            var fakeChain = new CertificateChain("fakeChain");
            var pfxBytes = new byte[] { 0xD, 0xE, 0xA, 0xD, 0xB, 0xE, 0xE, 0xF };
            const string fakePassword = "guaranteed to be chosen randomly";
            var targetResource = new Mock<ITargetResource>();
            var cert = new Mock<ICertificate>();
            var expiredCert = new Mock<ICertificate>();
            expiredCert.SetupGet(x => x.Expires)
                .Returns(DateTime.Now.AddDays(-99));

            // check if outdated (yes, found but expired)
            parser.Setup(x => x.ParseCertificateStore(config.Certificates[0]))
                .Returns(certStore.Object);
            certStore.Setup(x => x.GetCertificateAsync(CancellationToken.None))
                .Returns(Task.FromResult(expiredCert.Object));

            // let's encrypt challenge
            auth.Setup(x => x.AuthenticateAsync(config.Acme, CancellationToken.None))
                .Returns(Task.FromResult(authContext));
            ctx.Setup(x => x.NewOrder(config.Certificates[0].HostNames, null, null))
                .Returns(Task.FromResult(orderContext.Object));
            parser.Setup(x => x.ParseChallengeResponderAsync(config.Certificates[0], CancellationToken.None))
                .Returns(Task.FromResult(challengeResponder.Object));
            challengeResponder.Setup(x => x.InitiateChallengesAsync(orderContext.Object, CancellationToken.None))
                .Returns(Task.FromResult(new[] { challengeContext.Object }));
            challengeContext.Setup(x => x.Resource())
                .Returns(Task.FromResult(challenge));

            // save cert
            certBuilder.Setup(x => x.BuildCertificateAsync(orderContext.Object, config.Certificates[0], CancellationToken.None))
                .Returns(Task.FromResult((pfxBytes, fakePassword)));
            certStore.Setup(x => x.UploadAsync(pfxBytes, fakePassword, config.Certificates[0].HostNames, CancellationToken.None))
                .Returns(Task.FromResult(cert.Object));

            // update azure resource
            parser.Setup(x => x.ParseTargetResource(config.Certificates[0]))
                .Returns(targetResource.Object);

            // actual run - must run through all steps as cert is expired
            var r = await service.RenewCertificateAsync(config.Acme, config.Certificates[0], CancellationToken.None);
            r.Should().Be(RenewalResult.Success);

            certStore.Verify(x => x.UploadAsync(pfxBytes, fakePassword, config.Certificates[0].HostNames, CancellationToken.None));
            targetResource.Verify(x => x.UpdateAsync(cert.Object, CancellationToken.None), Times.Once);
        }

        [Test]
        public async Task ValidCertificateShouldNotRequestNewCertificateAndSkipResourceUpdateIfTargetDoesNotSupportIt()
        {
            var config = TestHelper.LoadConfig("config");
            var auth = new Mock<IAuthenticationService>();
            var certBuilder = new Mock<ICertificateBuilder>();
            var ctx = new Mock<IAcmeContext>();
            var orderContext = new Mock<IOrderContext>();
            var authContext = new AuthenticationContext(ctx.Object, config.Acme);
            var parser = new Mock<IRenewalOptionParser>();
            var log = new Mock<ILogger<RenewalService>>();
            IRenewalService service = new RenewalService(auth.Object, parser.Object, certBuilder.Object, log.Object);
            var certStore = new Mock<ICertificateStore>();
            var validCert = new Mock<ICertificate>();
            validCert.SetupGet(x => x.Expires)
                .Returns(DateTime.Now.AddDays(config.Acme.RenewXDaysBeforeExpiry + 1));
            validCert.SetupGet(x => x.HostNames)
                .Returns(config.Certificates[0].HostNames);

            var resource = new Mock<ITargetResource>();
            resource.SetupGet(x => x.SupportsCertificateCheck)
                .Returns(false);
            // check if outdated (no, found and valid)
            parser.Setup(x => x.ParseCertificateStore(config.Certificates[0]))
                .Returns(certStore.Object);
            parser.Setup(x => x.ParseTargetResource(config.Certificates[0]))
                .Returns(resource.Object);
            certStore.Setup(x => x.GetCertificateAsync(CancellationToken.None))
                .Returns(Task.FromResult(validCert.Object));

            // actual run - should skip everything as cert is still valid (and assumed to be deployed already)
            var r = await service.RenewCertificateAsync(config.Acme, config.Certificates[0], CancellationToken.None);
            r.Should().Be(RenewalResult.NoChange);

            certStore.Verify(x => x.UploadAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task ValidCertificateShouldNotRequestNewCertificateAndSkipResourceUpdateIfTargetIsUpToDate()
        {
            var config = TestHelper.LoadConfig("config");
            var auth = new Mock<IAuthenticationService>();
            var certBuilder = new Mock<ICertificateBuilder>();
            var ctx = new Mock<IAcmeContext>();
            var orderContext = new Mock<IOrderContext>();
            var authContext = new AuthenticationContext(ctx.Object, config.Acme);
            var parser = new Mock<IRenewalOptionParser>();
            var log = new Mock<ILogger<RenewalService>>();
            IRenewalService service = new RenewalService(auth.Object, parser.Object, certBuilder.Object, log.Object);
            var certStore = new Mock<ICertificateStore>();
            var validCert = new Mock<ICertificate>();
            validCert.SetupGet(x => x.Expires)
                .Returns(DateTime.Now.AddDays(config.Acme.RenewXDaysBeforeExpiry + 1));
            validCert.SetupGet(x => x.HostNames)
                .Returns(config.Certificates[0].HostNames);

            var resource = new Mock<ITargetResource>();
            resource.SetupGet(x => x.SupportsCertificateCheck)
                .Returns(true);
            resource.Setup(x => x.IsUsingCertificateAsync(validCert.Object, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(true));
            // check if outdated (no, found and valid)
            parser.Setup(x => x.ParseCertificateStore(config.Certificates[0]))
                .Returns(certStore.Object);
            parser.Setup(x => x.ParseTargetResource(config.Certificates[0]))
                .Returns(resource.Object);
            certStore.Setup(x => x.GetCertificateAsync(CancellationToken.None))
                .Returns(Task.FromResult(validCert.Object));

            // actual run - should skip everything as cert is still valid (and assumed to be deployed already)
            var r = await service.RenewCertificateAsync(config.Acme, config.Certificates[0], CancellationToken.None);
            r.Should().Be(RenewalResult.NoChange);

            certStore.Verify(x => x.UploadAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        /// <summary>
        /// Instead of silently assuming that resource is using latest cert must check with the resource.
        /// This fixes issues when cert issuance worked but deployment failed.
        /// Manual workaround would be to use updateResource = true
        /// https://github.com/MarcStan/lets-encrypt-azure/issues/6
        /// </summary>
        /// <returns></returns>
        [Test]
        public async Task IfLatestCertificateIsNotInUseThenResourceShouldBeUpdated()
        {
            var config = TestHelper.LoadConfig("config");
            var auth = new Mock<IAuthenticationService>();
            var certBuilder = new Mock<ICertificateBuilder>();
            var ctx = new Mock<IAcmeContext>();
            var orderContext = new Mock<IOrderContext>();
            var authContext = new AuthenticationContext(ctx.Object, config.Acme);
            var parser = new Mock<IRenewalOptionParser>();
            var log = new Mock<ILogger<RenewalService>>();
            IRenewalService service = new RenewalService(auth.Object, parser.Object, certBuilder.Object, log.Object);
            var certStore = new Mock<ICertificateStore>();
            var targetResource = new Mock<ITargetResource>();
            var validCert = new Mock<ICertificate>();
            targetResource.SetupGet(x => x.SupportsCertificateCheck)
                .Returns(true);
            targetResource.Setup(x => x.IsUsingCertificateAsync(validCert.Object, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(false));
            validCert.SetupGet(x => x.Expires)
                .Returns(DateTime.Now.AddDays(config.Acme.RenewXDaysBeforeExpiry + 1));
            validCert.SetupGet(x => x.HostNames)
                .Returns(config.Certificates[0].HostNames);

            // check if outdated (no, found and valid)
            parser.Setup(x => x.ParseCertificateStore(config.Certificates[0]))
                .Returns(certStore.Object);
            certStore.Setup(x => x.GetCertificateAsync(CancellationToken.None))
                .Returns(Task.FromResult(validCert.Object));

            // update azure resource
            parser.Setup(x => x.ParseTargetResource(config.Certificates[0]))
                .Returns(targetResource.Object);

            // actual run - must update resource with existing cert
            // as the cert is not yet in use
            var r = await service.RenewCertificateAsync(config.Acme, config.Certificates[0], CancellationToken.None);
            r.Should().Be(RenewalResult.Success);

            certStore.Verify(x => x.UploadAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()), Times.Never);
            targetResource.Verify(x => x.UpdateAsync(validCert.Object, CancellationToken.None), Times.Once);
        }

        [Test]
        public async Task NewCertificateShouldBeIssuedWhenOverrideIsUsed()
        {
            var config = TestHelper.LoadConfig("config");
            var auth = new Mock<IAuthenticationService>();
            var certBuilder = new Mock<ICertificateBuilder>();
            var ctx = new Mock<IAcmeContext>();
            var orderContext = new Mock<IOrderContext>();
            var authContext = new AuthenticationContext(ctx.Object, config.Acme);
            var parser = new Mock<IRenewalOptionParser>();
            var log = new Mock<ILogger<RenewalService>>();
            IRenewalService service = new RenewalService(auth.Object, parser.Object, certBuilder.Object, log.Object);
            var certStore = new Mock<ICertificateStore>();
            var challengeResponder = new Mock<IChallengeResponder>();
            var challengeContext = new Mock<IChallengeContext>();
            var challenge = new Challenge
            {
                Status = ChallengeStatus.Valid
            };
            var fakeChain = new CertificateChain("fakeChain");
            var pfxBytes = new byte[] { 0xD, 0xE, 0xA, 0xD, 0xB, 0xE, 0xE, 0xF };
            const string fakePassword = "guaranteed to be chosen randomly";
            var targetResource = new Mock<ITargetResource>();
            var cert = new Mock<ICertificate>();
            var validCert = new Mock<ICertificate>();
            validCert.SetupGet(x => x.Expires)
                .Returns(DateTime.Now.AddDays(config.Acme.RenewXDaysBeforeExpiry + 1));

            // check if outdated (no, found and valid)
            parser.Setup(x => x.ParseCertificateStore(config.Certificates[0]))
                .Returns(certStore.Object);
            certStore.Setup(x => x.GetCertificateAsync(CancellationToken.None))
                .Returns(Task.FromResult(validCert.Object));

            // let's encrypt challenge
            auth.Setup(x => x.AuthenticateAsync(config.Acme, CancellationToken.None))
                .Returns(Task.FromResult(authContext));
            ctx.Setup(x => x.NewOrder(config.Certificates[0].HostNames, null, null))
                .Returns(Task.FromResult(orderContext.Object));
            parser.Setup(x => x.ParseChallengeResponderAsync(config.Certificates[0], CancellationToken.None))
                .Returns(Task.FromResult(challengeResponder.Object));
            challengeResponder.Setup(x => x.InitiateChallengesAsync(orderContext.Object, CancellationToken.None))
                .Returns(Task.FromResult(new[] { challengeContext.Object }));
            challengeContext.Setup(x => x.Resource())
                .Returns(Task.FromResult(challenge));

            // save cert
            certBuilder.Setup(x => x.BuildCertificateAsync(orderContext.Object, config.Certificates[0], CancellationToken.None))
                .Returns(Task.FromResult((pfxBytes, fakePassword)));
            certStore.Setup(x => x.UploadAsync(pfxBytes, fakePassword, config.Certificates[0].HostNames, CancellationToken.None))
                .Returns(Task.FromResult(cert.Object));

            // update azure resource
            parser.Setup(x => x.ParseTargetResource(config.Certificates[0]))
                .Returns(targetResource.Object);

            // actual run - must run through everything despite valid cert due to override parameter
            config.Certificates[0].Overrides = new Overrides
            {
                ForceNewCertificates = true
            };
            var r = await service.RenewCertificateAsync(config.Acme, config.Certificates[0], CancellationToken.None);
            r.Should().Be(RenewalResult.Success);

            certStore.Verify(x => x.UploadAsync(pfxBytes, fakePassword, config.Certificates[0].HostNames, CancellationToken.None));
            targetResource.Verify(x => x.UpdateAsync(cert.Object, CancellationToken.None), Times.Once);
        }

        [Test]
        public async Task NewCertificateShouldBeIssuedWhenDomainListChangesEntries()
        {
            var config = TestHelper.LoadConfig("config");
            var auth = new Mock<IAuthenticationService>();
            var certBuilder = new Mock<ICertificateBuilder>();
            var ctx = new Mock<IAcmeContext>();
            var orderContext = new Mock<IOrderContext>();
            var authContext = new AuthenticationContext(ctx.Object, config.Acme);
            var parser = new Mock<IRenewalOptionParser>();
            var log = new Mock<ILogger<RenewalService>>();
            IRenewalService service = new RenewalService(auth.Object, parser.Object, certBuilder.Object, log.Object);
            var certStore = new Mock<ICertificateStore>();
            var challengeResponder = new Mock<IChallengeResponder>();
            var challengeContext = new Mock<IChallengeContext>();
            var challenge = new Challenge
            {
                Status = ChallengeStatus.Valid
            };
            var fakeChain = new CertificateChain("fakeChain");
            var pfxBytes = new byte[] { 0xD, 0xE, 0xA, 0xD, 0xB, 0xE, 0xE, 0xF };
            const string fakePassword = "guaranteed to be chosen randomly";
            var targetResource = new Mock<ITargetResource>();
            var cert = new Mock<ICertificate>();
            var validCert = new Mock<ICertificate>();
            validCert.SetupGet(x => x.Expires)
                .Returns(DateTime.Now.AddDays(config.Acme.RenewXDaysBeforeExpiry + 1));
            validCert.SetupGet(x => x.HostNames)
                // add another domain which should force a cert renewal
                .Returns(new[] { "example.com", "www.example.com", "blog.example.com" });

            // check if outdated (no, found and valid)
            parser.Setup(x => x.ParseCertificateStore(config.Certificates[0]))
                .Returns(certStore.Object);
            certStore.Setup(x => x.GetCertificateAsync(CancellationToken.None))
                .Returns(Task.FromResult(validCert.Object));

            // let's encrypt challenge
            auth.Setup(x => x.AuthenticateAsync(config.Acme, CancellationToken.None))
                .Returns(Task.FromResult(authContext));
            ctx.Setup(x => x.NewOrder(config.Certificates[0].HostNames, null, null))
                .Returns(Task.FromResult(orderContext.Object));
            parser.Setup(x => x.ParseChallengeResponderAsync(config.Certificates[0], CancellationToken.None))
                .Returns(Task.FromResult(challengeResponder.Object));
            challengeResponder.Setup(x => x.InitiateChallengesAsync(orderContext.Object, CancellationToken.None))
                .Returns(Task.FromResult(new[] { challengeContext.Object }));
            challengeContext.Setup(x => x.Resource())
                .Returns(Task.FromResult(challenge));

            // save cert
            certBuilder.Setup(x => x.BuildCertificateAsync(orderContext.Object, config.Certificates[0], CancellationToken.None))
                .Returns(Task.FromResult((pfxBytes, fakePassword)));
            certStore.Setup(x => x.UploadAsync(pfxBytes, fakePassword, config.Certificates[0].HostNames, CancellationToken.None))
                .Returns(Task.FromResult(cert.Object));

            // update azure resource
            parser.Setup(x => x.ParseTargetResource(config.Certificates[0]))
                .Returns(targetResource.Object);

            var r = await service.RenewCertificateAsync(config.Acme, config.Certificates[0], CancellationToken.None);
            r.Should().Be(RenewalResult.Success);

            certStore.Verify(x => x.UploadAsync(pfxBytes, fakePassword, config.Certificates[0].HostNames, CancellationToken.None));
            targetResource.Verify(x => x.UpdateAsync(cert.Object, CancellationToken.None), Times.Once);
        }

        [Test]
        public async Task NoNewCertificateShouldBeIssuedWhenDomainListChangesOrder()
        {
            var config = TestHelper.LoadConfig("config");
            var auth = new Mock<IAuthenticationService>();
            var certBuilder = new Mock<ICertificateBuilder>();
            var ctx = new Mock<IAcmeContext>();
            var orderContext = new Mock<IOrderContext>();
            var authContext = new AuthenticationContext(ctx.Object, config.Acme);
            var parser = new Mock<IRenewalOptionParser>();
            var log = new Mock<ILogger<RenewalService>>();
            IRenewalService service = new RenewalService(auth.Object, parser.Object, certBuilder.Object, log.Object);
            var certStore = new Mock<ICertificateStore>();
            var challengeResponder = new Mock<IChallengeResponder>();
            var challengeContext = new Mock<IChallengeContext>();
            var challenge = new Challenge
            {
                Status = ChallengeStatus.Valid
            };
            var fakeChain = new CertificateChain("fakeChain");
            var pfxBytes = new byte[] { 0xD, 0xE, 0xA, 0xD, 0xB, 0xE, 0xE, 0xF };
            const string fakePassword = "guaranteed to be chosen randomly";
            var targetResource = new Mock<ITargetResource>();
            var cert = new Mock<ICertificate>();
            var validCert = new Mock<ICertificate>();
            validCert.SetupGet(x => x.Expires)
                .Returns(DateTime.Now.AddDays(config.Acme.RenewXDaysBeforeExpiry + 1));
            validCert.SetupGet(x => x.HostNames)
                // config expects root first but this should not cause cert reissuance
                .Returns(new[] { "www.example.com", "example.com" });

            // check if outdated (no, found and valid)
            parser.Setup(x => x.ParseCertificateStore(config.Certificates[0]))
                .Returns(certStore.Object);
            certStore.Setup(x => x.GetCertificateAsync(CancellationToken.None))
                .Returns(Task.FromResult(validCert.Object));

            // let's encrypt challenge
            auth.Setup(x => x.AuthenticateAsync(config.Acme, CancellationToken.None))
                .Returns(Task.FromResult(authContext));
            ctx.Setup(x => x.NewOrder(config.Certificates[0].HostNames, null, null))
                .Returns(Task.FromResult(orderContext.Object));
            parser.Setup(x => x.ParseChallengeResponderAsync(config.Certificates[0], CancellationToken.None))
                .Returns(Task.FromResult(challengeResponder.Object));
            challengeResponder.Setup(x => x.InitiateChallengesAsync(orderContext.Object, CancellationToken.None))
                .Returns(Task.FromResult(new[] { challengeContext.Object }));
            challengeContext.Setup(x => x.Resource())
                .Returns(Task.FromResult(challenge));

            // save cert
            certBuilder.Setup(x => x.BuildCertificateAsync(orderContext.Object, config.Certificates[0], CancellationToken.None))
                .Returns(Task.FromResult((pfxBytes, fakePassword)));
            certStore.Setup(x => x.UploadAsync(pfxBytes, fakePassword, config.Certificates[0].HostNames, CancellationToken.None))
                .Returns(Task.FromResult(cert.Object));

            // update azure resource
            parser.Setup(x => x.ParseTargetResource(config.Certificates[0]))
                .Returns(targetResource.Object);

            var r = await service.RenewCertificateAsync(config.Acme, config.Certificates[0], CancellationToken.None);
            r.Should().Be(RenewalResult.NoChange);

            certStore.Verify(x => x.UploadAsync(pfxBytes, fakePassword, config.Certificates[0].HostNames, CancellationToken.None), Times.Never);
            targetResource.Verify(x => x.UpdateAsync(cert.Object, CancellationToken.None), Times.Never);
        }

        [Test]
        public async Task NoNewCertificateShouldBeIssuedWhenOverrideIsUsedButDomainlistNotMatched()
        {
            var config = TestHelper.LoadConfig("config");
            var auth = new Mock<IAuthenticationService>();
            var certBuilder = new Mock<ICertificateBuilder>();
            var ctx = new Mock<IAcmeContext>();
            var orderContext = new Mock<IOrderContext>();
            var authContext = new AuthenticationContext(ctx.Object, config.Acme);
            var parser = new Mock<IRenewalOptionParser>();
            var log = new Mock<ILogger<RenewalService>>();
            IRenewalService service = new RenewalService(auth.Object, parser.Object, certBuilder.Object, log.Object);
            var certStore = new Mock<ICertificateStore>();
            var challengeResponder = new Mock<IChallengeResponder>();
            var challengeContext = new Mock<IChallengeContext>();
            var challenge = new Challenge
            {
                Status = ChallengeStatus.Valid
            };
            var fakeChain = new CertificateChain("fakeChain");
            var pfxBytes = new byte[] { 0xD, 0xE, 0xA, 0xD, 0xB, 0xE, 0xE, 0xF };
            const string fakePassword = "guaranteed to be chosen randomly";
            var targetResource = new Mock<ITargetResource>();
            var cert = new Mock<ICertificate>();
            var validCert = new Mock<ICertificate>();
            validCert.SetupGet(x => x.Expires)
                .Returns(DateTime.Now.AddDays(config.Acme.RenewXDaysBeforeExpiry + 1));
            validCert.SetupGet(x => x.HostNames)
                // config expects root first but this should not cause cert reissuance
                .Returns(new[] { "www.example.com", "example.com" });

            // check if outdated (no, found and valid)
            parser.Setup(x => x.ParseCertificateStore(config.Certificates[0]))
                .Returns(certStore.Object);
            certStore.Setup(x => x.GetCertificateAsync(CancellationToken.None))
                .Returns(Task.FromResult(validCert.Object));

            // let's encrypt challenge
            auth.Setup(x => x.AuthenticateAsync(config.Acme, CancellationToken.None))
                .Returns(Task.FromResult(authContext));
            ctx.Setup(x => x.NewOrder(config.Certificates[0].HostNames, null, null))
                .Returns(Task.FromResult(orderContext.Object));
            parser.Setup(x => x.ParseChallengeResponderAsync(config.Certificates[0], CancellationToken.None))
                .Returns(Task.FromResult(challengeResponder.Object));
            challengeResponder.Setup(x => x.InitiateChallengesAsync(orderContext.Object, CancellationToken.None))
                .Returns(Task.FromResult(new[] { challengeContext.Object }));
            challengeContext.Setup(x => x.Resource())
                .Returns(Task.FromResult(challenge));

            // save cert
            certBuilder.Setup(x => x.BuildCertificateAsync(orderContext.Object, config.Certificates[0], CancellationToken.None))
                .Returns(Task.FromResult((pfxBytes, fakePassword)));
            certStore.Setup(x => x.UploadAsync(pfxBytes, fakePassword, config.Certificates[0].HostNames, CancellationToken.None))
                .Returns(Task.FromResult(cert.Object));

            // update azure resource
            parser.Setup(x => x.ParseTargetResource(config.Certificates[0]))
                .Returns(targetResource.Object);

            // actual run - must run through everything despite valid cert due to override parameter
            config.Certificates[0].Overrides = new Overrides
            {
                ForceNewCertificates = true,
                DomainsToUpdate = new[] { "notadomain-fromthelist.example.com" }
            };
            var r = await service.RenewCertificateAsync(config.Acme, config.Certificates[0], CancellationToken.None);
            r.Should().Be(RenewalResult.NoChange);

            certStore.Verify(x => x.UploadAsync(pfxBytes, fakePassword, config.Certificates[0].HostNames, CancellationToken.None), Times.Never);
            targetResource.Verify(x => x.UpdateAsync(cert.Object, CancellationToken.None), Times.Never);
        }

        [Test]
        public async Task NewCertificateShouldBeIssuedWhenOverrideIsUsedAndDomainlistIsMatched()
        {
            var config = TestHelper.LoadConfig("config");
            var auth = new Mock<IAuthenticationService>();
            var certBuilder = new Mock<ICertificateBuilder>();
            var ctx = new Mock<IAcmeContext>();
            var orderContext = new Mock<IOrderContext>();
            var authContext = new AuthenticationContext(ctx.Object, config.Acme);
            var parser = new Mock<IRenewalOptionParser>();
            var log = new Mock<ILogger<RenewalService>>();
            IRenewalService service = new RenewalService(auth.Object, parser.Object, certBuilder.Object, log.Object);
            var certStore = new Mock<ICertificateStore>();
            var challengeResponder = new Mock<IChallengeResponder>();
            var challengeContext = new Mock<IChallengeContext>();
            var challenge = new Challenge
            {
                Status = ChallengeStatus.Valid
            };
            var fakeChain = new CertificateChain("fakeChain");
            var pfxBytes = new byte[] { 0xD, 0xE, 0xA, 0xD, 0xB, 0xE, 0xE, 0xF };
            const string fakePassword = "guaranteed to be chosen randomly";
            var targetResource = new Mock<ITargetResource>();
            var cert = new Mock<ICertificate>();
            var validCert = new Mock<ICertificate>();
            validCert.SetupGet(x => x.Expires)
                .Returns(DateTime.Now.AddDays(config.Acme.RenewXDaysBeforeExpiry + 1));

            // check if outdated (no, found and valid)
            parser.Setup(x => x.ParseCertificateStore(config.Certificates[0]))
                .Returns(certStore.Object);
            certStore.Setup(x => x.GetCertificateAsync(CancellationToken.None))
                .Returns(Task.FromResult(validCert.Object));

            // let's encrypt challenge
            auth.Setup(x => x.AuthenticateAsync(config.Acme, CancellationToken.None))
                .Returns(Task.FromResult(authContext));
            ctx.Setup(x => x.NewOrder(config.Certificates[0].HostNames, null, null))
                .Returns(Task.FromResult(orderContext.Object));
            parser.Setup(x => x.ParseChallengeResponderAsync(config.Certificates[0], CancellationToken.None))
                .Returns(Task.FromResult(challengeResponder.Object));
            challengeResponder.Setup(x => x.InitiateChallengesAsync(orderContext.Object, CancellationToken.None))
                .Returns(Task.FromResult(new[] { challengeContext.Object }));
            challengeContext.Setup(x => x.Resource())
                .Returns(Task.FromResult(challenge));

            // save cert
            certBuilder.Setup(x => x.BuildCertificateAsync(orderContext.Object, config.Certificates[0], CancellationToken.None))
                .Returns(Task.FromResult((pfxBytes, fakePassword)));
            certStore.Setup(x => x.UploadAsync(pfxBytes, fakePassword, config.Certificates[0].HostNames, CancellationToken.None))
                .Returns(Task.FromResult(cert.Object));

            // update azure resource
            parser.Setup(x => x.ParseTargetResource(config.Certificates[0]))
                .Returns(targetResource.Object);

            // actual run - must run through everything despite valid cert due to override parameter
            config.Certificates[0].Overrides = new Overrides
            {
                ForceNewCertificates = true,
                DomainsToUpdate = new[] { "example.com" }
            };
            var r = await service.RenewCertificateAsync(config.Acme, config.Certificates[0], CancellationToken.None);
            r.Should().Be(RenewalResult.Success);

            certStore.Verify(x => x.UploadAsync(pfxBytes, fakePassword, config.Certificates[0].HostNames, CancellationToken.None));
            targetResource.Verify(x => x.UpdateAsync(cert.Object, CancellationToken.None), Times.Once);
        }
    }
}
