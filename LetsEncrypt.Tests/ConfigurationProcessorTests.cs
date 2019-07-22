using FluentAssertions;
using LetsEncrypt.Logic.Config;
using NUnit.Framework;
using System;
using System.IO;

namespace LetsEncrypt.Tests
{
    public class ConfigurationProcessorTests
    {
        [Test]
        public void LoadingConfigShouldFailIfParametersAreMissing()
        {
            IConfigurationProcessor processor = new ConfigurationProcessor();
            var content = File.ReadAllText("Files/invalid.json");
            new Action(() => processor.ValidateAndLoad(content)).Should().Throw<ArgumentException>();
        }

        [Test]
        public void LoadingConfigWithDefaults()
        {
            IConfigurationProcessor processor = new ConfigurationProcessor();
            var content = File.ReadAllText("Files/config.json");
            var cfg = processor.ValidateAndLoad(content);
            cfg.Acme.Email.Should().Be("you@example.com");
            cfg.Acme.Staging.Should().BeFalse();
            cfg.Acme.RenewXDaysBeforeExpiry.Should().Be(30);
            cfg.Certificates.Should().HaveCount(1);

            var cert = cfg.Certificates[0];
            cert.HostNames.Should().BeEquivalentTo(new[]
            {
                "example.com", "www.example.com"
            });
            cert.CertificateStore.Should().BeNull();
            cert.ChallengeResponder.Should().BeNull();

            cert.TargetResource.Type.Should().Be("cdn");
            cert.TargetResource.Name.Should().Be("example");
        }

        [Test]
        public void LoadingAppServiceConfig()
        {
            IConfigurationProcessor processor = new ConfigurationProcessor();
            var content = File.ReadAllText("Files/appservice.json");
            var cfg = processor.ValidateAndLoad(content);
            cfg.Acme.Email.Should().Be("you@example.com");
            cfg.Acme.Staging.Should().BeFalse();
            cfg.Acme.RenewXDaysBeforeExpiry.Should().Be(30);
            cfg.Certificates.Should().HaveCount(1);

            var cert = cfg.Certificates[0];
            cert.HostNames.Should().BeEquivalentTo(new[]
            {
                "example.com", "www.example.com"
            });
            cert.CertificateStore.Should().BeNull();
            cert.ChallengeResponder.Should().BeNull();

            cert.TargetResource.Type.Should().Be("appService");
            cert.TargetResource.Name.Should().Be("example");
        }
    }
}
