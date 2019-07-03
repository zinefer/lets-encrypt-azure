using LetsEncrypt.Logic;
using LetsEncrypt.Logic.Acme;
using LetsEncrypt.Logic.Authentication;
using LetsEncrypt.Logic.Config;
using LetsEncrypt.Logic.Renewal;
using LetsEncrypt.Logic.Storage;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System;
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

            // TODO: load from config
            CertificateConfiguration settings = null;
            IStorageProvider httpChallengeStorageProvider = new AzureBlobStorageProvider(settings.StorageAccountConnectionString, "$web");

            var options = new AcmeOptions(true)
            {
                Email = Environment.GetEnvironmentVariable("Email")
            };

            IAuthenticationService authenticationService = new AuthenticationService(storageProvider);

            var authContext = await authenticationService.AuthenticateAsync(options, cancellationToken);

            var tokenProvider = new AzureServiceTokenProvider();
            var kvClient = new Microsoft.Azure.KeyVault.KeyVaultClient(new Microsoft.Azure.KeyVault.KeyVaultClient.AuthenticationCallback(tokenProvider.KeyVaultTokenCallback));

            var cred = new MsiTokenProvider(tokenProvider, _ => "https://management.azure.com");
            var cdnClient = new Microsoft.Azure.Management.Cdn.CdnManagementClient(cred);
            IRenewalService renewalService = new RenewalService(new AzureStorageHttpChallengeService(httpChallengeStorageProvider), kvClient, cdnClient);

            var certificateBytes = await renewalService.RenewCertificateAsync(settings, authContext, cancellationToken);
        }
    }
}
