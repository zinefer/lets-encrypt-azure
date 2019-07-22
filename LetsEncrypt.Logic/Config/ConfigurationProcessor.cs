using LetsEncrypt.Logic.Config.Properties;
using LetsEncrypt.Logic.Extensions;
using Newtonsoft.Json;
using System;

namespace LetsEncrypt.Logic.Config
{
    public class ConfigurationProcessor : IConfigurationProcessor
    {
        public Configuration ValidateAndLoad(string json)
        {
            var instance = JsonConvert.DeserializeObject<Configuration>(json);
            // give helpful error messages to user
            if (instance.Acme == null)
                throw new ArgumentException($"Missing config section {nameof(instance.Acme)}");
            if (string.IsNullOrWhiteSpace(instance.Acme.Email))
                throw new ArgumentException($"Missing {nameof(instance.Acme.Email)} in section {nameof(instance.Acme)}");
            if (instance.Acme.RenewXDaysBeforeExpiry <= 1)
                throw new ArgumentException($"Renewal is set to {instance.Acme.RenewXDaysBeforeExpiry} which is not valid. " +
                    "Recommendation is to set it to at least 2 days, as the certificate update may take up to 10 hours for the CDN.");
            if (instance.Acme.RenewXDaysBeforeExpiry >= 90)
                throw new ArgumentException($"Renewal is set to {instance.Acme.RenewXDaysBeforeExpiry} which is not valid." +
                    "Let's Encrypt certificates are only valid for 90 days, a setting this high would cause renewals every single day. Let's Encrypt recommendation is to renew after 1/3 lifetime, so after 30 days.");

            for (int i = 0; i < instance.Certificates.Length; i++)
            {
                var cfg = instance.Certificates[i];
                if (cfg.HostNames.IsNullOrEmpty())
                    throw new ArgumentNullException($"Missing hostnames in certificate section (index: {i})");

                if (cfg.ChallengeResponder != null)
                {
                    if (string.IsNullOrEmpty(cfg.ChallengeResponder.Type))
                    {
                        throw new ArgumentException($"Missing parameter type in section {nameof(cfg.ChallengeResponder)} of {cfg.HostNames}");
                    }
                    var validator = GetChallengeVerificationType(cfg.ChallengeResponder.Type);
                    validator(cfg.ChallengeResponder);
                }

                if (cfg.CertificateStore != null)
                {
                    if (string.IsNullOrEmpty(cfg.CertificateStore.Type))
                        throw new ArgumentException($"Missing parameter type in section {nameof(cfg.CertificateStore)} of {cfg.HostNames}");

                    var validator = GetCertificateStoreType(cfg.CertificateStore.Type);
                    validator(cfg.CertificateStore);
                }
                if (cfg.TargetResource == null)
                    throw new ArgumentException($"Missing {nameof(cfg.TargetResource)} section");
                if (string.IsNullOrEmpty(cfg.TargetResource.Type))
                    throw new ArgumentException($"Missing parameter type in section {nameof(cfg.TargetResource)} of {cfg.HostNames}");

                var validate = GetTargetResourceType(cfg.TargetResource.Type);
                cfg.Overrides = Overrides.None;
                validate(cfg.TargetResource);
            }

            return instance;
        }

        private Action<GenericEntry> GetChallengeVerificationType(string type)
        {
            switch (type.ToLowerInvariant())
            {
                case "storageaccount":
                    return ValidateStorage;
                default:
                    throw new ArgumentException($"Unsupported type {type} for challengeProvider");
            }
        }

        private Action<GenericEntry> GetCertificateStoreType(string type)
        {
            switch (type.ToLowerInvariant())
            {
                case "keyvault":
                    return ValidateKeyVault;
                default:
                    throw new ArgumentException($"Unsupported type {type} for certificateStore");
            }
        }

        private Action<GenericEntry> GetTargetResourceType(string type)
        {
            switch (type.ToLowerInvariant())
            {
                case "cdn":
                    return ValidateCdn;
                default:
                    throw new ArgumentException($"Unsupported type {type} for targetResource");
            }
        }

        private void ValidateStorage(GenericEntry entry)
        {
            var props = entry.Properties.ToObject<StorageProperties>();
            if (!string.IsNullOrEmpty(props.ConnectionString))
            {
                if (string.IsNullOrEmpty(props.ContainerName))
                    throw new ArgumentException($"{nameof(props.ContainerName)} must be set for challenge storageAccount");

                if (!string.IsNullOrEmpty(props.KeyVaultName) ||
                    props.SecretName != "Storage")
                    throw new ArgumentException($"storageAccount challenge provider either requires {nameof(props.ConnectionString)} or {nameof(props.KeyVaultName)}+{nameof(props.SecretName)}. Cannot have both at once");
            }
        }

        private void ValidateKeyVault(GenericEntry entry)
        {
            // no validation required here, everything can be null and use fallbacks
        }

        private void ValidateCdn(GenericEntry entry)
        {
            if (entry.Properties != null)
            {
                var props = entry.Properties.ToObject<CdnProperties>();
                if (string.IsNullOrEmpty(props.Name))
                    throw new ArgumentException("cdn requires name to be set");
            }
            else
            {
                if (string.IsNullOrEmpty(entry.Name))
                    throw new ArgumentException($"Either {nameof(entry.Name)} or {nameof(entry.Properties)} must be set");
            }
        }
    }
}
