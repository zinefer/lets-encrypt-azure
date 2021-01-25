﻿using Azure;
using LetsEncrypt.Logic.Azure;
using LetsEncrypt.Logic.Config.Properties;
using LetsEncrypt.Logic.Extensions;
using LetsEncrypt.Logic.Providers.CertificateStores;
using LetsEncrypt.Logic.Providers.ChallengeResponders;
using LetsEncrypt.Logic.Providers.TargetResources;
using LetsEncrypt.Logic.Storage;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace LetsEncrypt.Logic.Config
{
    public class RenewalOptionParser : IRenewalOptionParser
    {
        public const string FileNameForPermissionCheck = "permission-check.blob";

        private readonly IAzureHelper _azureHelper;
        private readonly ILogger _logger;
        private readonly IStorageFactory _storageFactory;
        private readonly IAzureAppServiceClient _azureAppServiceClient;
        private readonly IAzureCdnClient _azureCdnClient;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IKeyVaultFactory _keyVaultFactory;

        public RenewalOptionParser(
            IAzureHelper azureHelper,
            IKeyVaultFactory keyVaultFactory,
            IStorageFactory storageFactory,
            IAzureAppServiceClient azureAppServiceClient,
            IAzureCdnClient azureCdnClient,
            ILoggerFactory loggerFactory)
        {
            _azureHelper = azureHelper;
            _keyVaultFactory = keyVaultFactory;
            _storageFactory = storageFactory;
            _azureAppServiceClient = azureAppServiceClient;
            _azureCdnClient = azureCdnClient;
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<RenewalOptionParser>();
        }

        public async Task<IChallengeResponder> ParseChallengeResponderAsync(CertificateRenewalOptions cfg, CancellationToken cancellationToken)
        {
            var certStore = ParseCertificateStore(cfg);
            var target = ParseTargetResource(cfg);
            var cr = cfg.ChallengeResponder ?? new GenericEntry
            {
                Type = "storageAccount",
                Properties = JObject.FromObject(new StorageProperties
                {
                    AccountName = ConvertToValidStorageAccountName(target.Name),
                    KeyVaultName = certStore.Name
                })
            };
            switch (cr.Type.ToLowerInvariant())
            {
                case "storageaccount":
                    var props = cr.Properties?.ToObject<StorageProperties>() ?? new StorageProperties
                    {
                        KeyVaultName = cr.Name,
                        AccountName = ConvertToValidStorageAccountName(cr.Name)
                    };

                    // try MSI first, must do check if we can read to know if we have access
                    var accountName = props.AccountName;
                    if (string.IsNullOrEmpty(accountName))
                        accountName = ConvertToValidStorageAccountName(target.Name);

                    var storage = await _storageFactory.FromMsiAsync(accountName, props.ContainerName, cancellationToken);
                    // verify that MSI access works, fallback otherwise
                    // not ideal since it's a readonly check
                    // -> we need Blob Contributor for challenge persist but user could set Blob Reader and this check would pass
                    // alternative: write + delete a file from container as a check
                    try
                    {
                        await storage.ExistsAsync(FileNameForPermissionCheck, cancellationToken);
                    }
                    catch (RequestFailedException e) when (e.Status == (int)HttpStatusCode.Forbidden)
                    {
                        _logger.LogWarning($"MSI access to storage {accountName} failed. Attempting fallbacks via connection string. (You can ignore this warning if you don't use MSI authentication).");
                        var connectionString = props.ConnectionString;
                        if (string.IsNullOrEmpty(connectionString))
                        {
                            // falback to secret in keyvault
                            var keyVaultName = props.KeyVaultName;
                            if (string.IsNullOrEmpty(keyVaultName))
                                keyVaultName = certStore.Name;

                            _logger.LogInformation($"No connection string in config, checking keyvault {keyVaultName} for secret {props.SecretName}");
                            try
                            {
                                connectionString = await GetSecretAsync(keyVaultName, props.SecretName, cancellationToken);
                            }
                            catch (Exception ex)
                            {
                                throw new AggregateException($"Failed to get connectionstring in secret {props.SecretName} from keyvault {keyVaultName}. If you intended to use storage MSI access, set \"Storage Blob Data Contributor\" on the respective storage container (permissions might take more than 10 minutes to take effect)", new[] { ex });
                            }
                        }
                        if (string.IsNullOrEmpty(connectionString))
                            throw new InvalidOperationException($"MSI access failed for {accountName} and could not find fallback connection string for storage access. Unable to proceed with Let's encrypt challenge");

                        storage = _storageFactory.FromConnectionString(connectionString, props.ContainerName);
                    }
                    return new AzureStorageHttpChallengeResponder(storage, props.Path);
                default:
                    throw new NotImplementedException(cr.Type);
            }
        }

        public ICertificateStore ParseCertificateStore(CertificateRenewalOptions cfg)
        {
            var target = ParseTargetResource(cfg);
            var store = cfg.CertificateStore ?? new GenericEntry
            {
                Type = "keyVault",
                Name = target.Name
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
                        certificateName = cfg.HostNames.First().Replace(".", "-");

                    var keyVaultName = props.Name;
                    if (string.IsNullOrEmpty(keyVaultName))
                        keyVaultName = target.Name;

                    var resourceGroupName = props.ResourceGroupName;
                    if (string.IsNullOrEmpty(resourceGroupName))
                        resourceGroupName = keyVaultName;

                    return new KeyVaultCertificateStore(_azureHelper, _keyVaultFactory, keyVaultName, resourceGroupName, certificateName);
                default:
                    throw new NotImplementedException(store.Type);
            }
        }

        public ITargetResource ParseTargetResource(CertificateRenewalOptions cfg)
        {
            switch (cfg.TargetResource.Type.ToLowerInvariant())
            {
                case "cdn":
                    {
                        var props = cfg.TargetResource.Properties == null
                            ? new CdnProperties
                            {
                                Endpoints = new[] { cfg.TargetResource.Name },
                                Name = cfg.TargetResource.Name,
                                ResourceGroupName = cfg.TargetResource.Name
                            }
                            : cfg.TargetResource.Properties.ToObject<CdnProperties>();

                        if (string.IsNullOrEmpty(props.Name))
                            throw new ArgumentException($"CDN section is missing required property {nameof(props.Name)}");

                        var rg = props.ResourceGroupName;
                        if (string.IsNullOrEmpty(rg))
                            rg = props.Name;
                        var endpoints = props.Endpoints;
                        if (endpoints.IsNullOrEmpty())
                            endpoints = new[] { props.Name };

                        return new CdnTargetResource(_azureCdnClient, rg, props.Name, endpoints, _loggerFactory.CreateLogger<CdnTargetResource>());
                    }
                case "appservice":
                    {
                        var props = cfg.TargetResource.Properties == null
                            ? new AppServiceProperties
                            {
                                Name = cfg.TargetResource.Name,
                                ResourceGroupName = cfg.TargetResource.Name
                            }
                            : cfg.TargetResource.Properties.ToObject<AppServiceProperties>();

                        if (string.IsNullOrEmpty(props.Name))
                            throw new ArgumentException($"AppService section is missing required property {nameof(props.Name)}");

                        var rg = props.ResourceGroupName;
                        if (string.IsNullOrEmpty(rg))
                            rg = props.Name;

                        return new AppServiceTargetResoure(_azureAppServiceClient, rg, props.Name, _loggerFactory.CreateLogger<AppServiceTargetResoure>());
                    }
                default:
                    throw new NotImplementedException(cfg.TargetResource.Type);
            }
        }

        /// <summary>
        /// Given a valid azure resource name converts it to the equivalent storage name by removing all dashes
        /// as per the usual convention used everywhere.
        /// </summary>
        /// <param name="resourceName"></param>
        private string ConvertToValidStorageAccountName(string resourceName)
            => resourceName?.Replace("-", "");

        private async Task<string> GetSecretAsync(string keyVaultName, string secretName, CancellationToken cancellationToken)
        {
            try
            {
                var secretClient = _keyVaultFactory.CreateSecretClient(keyVaultName);
                var secret = await secretClient.GetSecretAsync(secretName, null, cancellationToken);
                return secret.Value.Value;
            }
            catch (RequestFailedException ex)
            {
                if (ex.Status == (int)HttpStatusCode.Forbidden)
                {
                    _logger.LogError(ex, $"Access forbidden. Unable to get secret from keyvault {keyVaultName}");
                    throw;
                }
                if (ex.Status == (int)HttpStatusCode.NotFound)
                    return null;

                throw;
            }
            catch (HttpRequestException e)
            {
                _logger.LogError(e, $"Unable to get secret from keyvault {keyVaultName}");
                throw;
            }
        }
    }
}
