using LetsEncrypt.Logic;
using LetsEncrypt.Logic.Acme;
using LetsEncrypt.Logic.Authentication;
using LetsEncrypt.Logic.Config;
using LetsEncrypt.Logic.Renewal;
using LetsEncrypt.Logic.Storage;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace LetsEncrypt.Func
{
    public static class AutoRenewal
    {
        private const string Daily = "0 0 0 * * *";

        [FunctionName("renew")]
        public static async Task RenewAsync(
          [TimerTrigger(Daily, RunOnStartup = true)] TimerInfo timer,
          ILogger log,
          CancellationToken cancellationToken)
        {
            // internal storage (used for letsencrypt account metadata)
            IStorageProvider storageProvider = new AzureBlobStorageProvider(Environment.GetEnvironmentVariable("AzureWebJobsStorage"), "letsencrypt");

            var settings = await LoadConfigAsync(storageProvider, cancellationToken);
            var options = new AcmeOptions(true)
            {
                Email = Environment.GetEnvironmentVariable("Email")
            };
            var tokenProvider = new AzureServiceTokenProvider();
            var kvClient = new Microsoft.Azure.KeyVault.KeyVaultClient(new Microsoft.Azure.KeyVault.KeyVaultClient.AuthenticationCallback(tokenProvider.KeyVaultTokenCallback));
            var cred = new MsiTokenProvider(tokenProvider, "28196694-37de-4dc1-960f-90e77b8d6a56", _ => "https://management.azure.com/");

            foreach (var config in settings)
            {
                IStorageProvider httpChallengeStorageProvider = new AzureBlobStorageProvider(config.StorageAccountConnectionString, "$web");
                IAuthenticationService authenticationService = new AuthenticationService(storageProvider);
                var authContext = await authenticationService.AuthenticateAsync(options, cancellationToken);
                IRenewalService renewalService = new RenewalService(new AzureStorageHttpChallengeService(httpChallengeStorageProvider), kvClient, new HttpClient(cred), log);

                var hostnames = string.Join(";", config.HostNames);
                try
                {
                    var result = await renewalService.RenewCertificateAsync(config, authContext, cancellationToken);
                    switch (result)
                    {
                        case RenewalResult.NoChange:
                            log.LogInformation($"Certificate renewal skipped for: {hostnames} (no change required yet)");
                            break;
                        case RenewalResult.Success:
                            log.LogInformation($"Certificate renewal succeeded for: {hostnames}");
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(result.ToString());
                    }
                }
                catch (Exception e)
                {
                    log.LogError(e, $"Certificate renewal failed for: {hostnames}!");
                }
            }
        }

        private static async Task<IEnumerable<CertificateConfiguration>> LoadConfigAsync(IStorageProvider storageProvider, CancellationToken cancellationToken)
        {
            var configs = new List<CertificateConfiguration>();
            foreach (var path in await storageProvider.ListAsync("config/", cancellationToken))
            {
                var content = await storageProvider.GetAsync(path, cancellationToken);
                configs.Add(JsonConvert.DeserializeObject<CertificateConfiguration>(content));
            }
            return configs.ToArray();
        }
    }
}
