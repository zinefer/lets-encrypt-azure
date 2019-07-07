using LetsEncrypt.Logic.Config.Properties;
using LetsEncrypt.Logic.Extensions;
using LetsEncrypt.Logic.Providers.CertificateStores;
using LetsEncrypt.Logic.Providers.ChallengeResponders;
using LetsEncrypt.Logic.Providers.TargetResources;
using LetsEncrypt.Logic.Storage;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.Services.AppAuthentication;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LetsEncrypt.Logic.Config
{
    public class CertificateRenewalOptions : ICertificateRenewalOptions
    {
        /// <summary>
        /// The set of hostnames for which to issue a certificate.
        /// Note that these will all be issued into a single certificate.
        /// </summary>
        public string[] HostNames { get; set; }

        public GenericEntry ChallengeResponder { get; set; }

        public GenericEntry CertificateStore { get; set; }

        public GenericEntry TargetResource { get; set; }

        public async Task<IChallengeResponder> ParseChallengeResponderAsync(CancellationToken cancellationToken)
        {
            var cr = ChallengeResponder ?? new GenericEntry
            {
                Type = "storageAccount",
                Name = ParseCertificateStore().Name
            };
            switch (cr.Type.ToLowerInvariant())
            {
                case "storageaccount":
                    var props = cr.Properties?.ToObject<StorageProperties>() ?? new StorageProperties
                    {
                        KeyVaultName = cr.Name
                    };
                    var connectionString = props.ConnectionString;
                    if (string.IsNullOrEmpty(connectionString))
                    {
                        // falback to secret in keyvault
                        var kvClient = GetKeyVaultClient();
                        var keyVaultName = props.KeyVaultName;
                        if (string.IsNullOrEmpty(keyVaultName))
                        {
                            var certStore = ParseCertificateStore();
                            keyVaultName = certStore.Name;
                        }
                        var secret = await kvClient.GetSecretAsync($"https://{keyVaultName}.vault.azure.net", props.SecretName, cancellationToken);
                        connectionString = secret.Value;
                    }
                    var storage = new AzureBlobStorageProvider(connectionString, props.ContainerName);
                    return new AzureStorageHttpChallengeResponder(storage);
                default:
                    throw new NotImplementedException(cr.Type);
            }
        }

        public ICertificateStore ParseCertificateStore()
        {
            var store = CertificateStore ?? new GenericEntry
            {
                Type = "keyVault",
                Name = ParseTargetResource().Name
            };

            switch (store.Type.ToLowerInvariant())
            {
                case "keyvault":
                    // all optional
                    var props = store.Properties?.ToObject<KeyVaultProperties>() ?? new KeyVaultProperties
                    {
                        Name = store.Name
                    };
                    var certificateName = props.CertificateName;
                    if (string.IsNullOrEmpty(certificateName))
                        certificateName = HostNames.First().Replace(".", "-");
                    var keyVaultName = props.Name;
                    if (string.IsNullOrEmpty(keyVaultName))
                    {
                        var target = ParseTargetResource();
                        keyVaultName = target.Name;
                    }
                    var kvClient = GetKeyVaultClient();
                    return new KeyVaultCertificateStore(kvClient, keyVaultName, certificateName);
                default:
                    throw new NotImplementedException(store.Type);
            }
        }

        public ITargetResource ParseTargetResource()
        {
            switch (TargetResource.Type.ToLowerInvariant())
            {
                case "cdn":
                    var props = TargetResource.Properties == null
                        ? new CdnProperties
                        {
                            Endpoints = new[] { TargetResource.Name },
                            Name = TargetResource.Name,
                            ResourceGroupName = TargetResource.Name
                        }
                        : TargetResource.Properties.ToObject<CdnProperties>();

                    if (string.IsNullOrEmpty(props.Name))
                        throw new ArgumentException($"CDN section is missing required property {nameof(props.Name)}");

                    var rg = props.ResourceGroupName;
                    if (string.IsNullOrEmpty(rg))
                        rg = props.Name;
                    var endpoints = props.Endpoints;
                    if (endpoints.IsNullOrEmpty())
                        endpoints = new[] { props.Name };

                    return new CdnTargetResoure(rg, props.Name, endpoints);
                default:
                    throw new NotImplementedException(TargetResource.Type);
            }
        }

        private IKeyVaultClient GetKeyVaultClient()
        {
            var tokenProvider = new AzureServiceTokenProvider();
            return new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(tokenProvider.KeyVaultTokenCallback));
        }
    }
}
