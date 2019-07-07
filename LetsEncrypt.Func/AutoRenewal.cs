using LetsEncrypt.Logic.Authentication;
using LetsEncrypt.Logic.Config;
using LetsEncrypt.Logic.Renewal;
using LetsEncrypt.Logic.Storage;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace LetsEncrypt.Func
{
    public static class AutoRenewal
    {
        /// <summary>
        /// Wrapper function that allows manual execution via http
        /// </summary>
        /// <param name="req"></param>
        /// <param name="log"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [FunctionName("execute")]
        public static Task ExecuteManuallyAsync(
            [HttpTrigger(AuthorizationLevel.Function)] HttpRequestMessage req,
            ILogger log,
            CancellationToken cancellationToken)
            => RenewAsync(null, log, cancellationToken);

        /// <summary>
        /// Time triggered function that reads config files from storage
        /// and renews certificates accordingly if needed.
        /// </summary>
        /// <param name="timer"></param>
        /// <param name="log"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [FunctionName("renew")]
        public static async Task RenewAsync(
          [TimerTrigger(Schedule.Daily, RunOnStartup = true)] TimerInfo timer,
          ILogger log,
          CancellationToken cancellationToken)
        {
            // internal storage (used for letsencrypt account metadata)
            IStorageProvider storageProvider = new AzureBlobStorageProvider(Environment.GetEnvironmentVariable("AzureWebJobsStorage"), "letsencrypt");

            IConfigurationProcessor processor = new ConfigurationProcessor();
            var configurations = await LoadConfigFilesAsync(storageProvider, processor, log, cancellationToken);
            IAuthenticationService authenticationService = new AuthenticationService(storageProvider);

            IRenewalService renewalService = new RenewalService(authenticationService, log);
            foreach ((var name, var config) in configurations)
            {
                using (log.BeginScope($"Working on certificates from {name}"))
                {
                    foreach (var cert in config.Certificates)
                    {
                        var hostNames = string.Join(";", cert.HostNames);
                        try
                        {
                            var result = await renewalService.RenewCertificateAsync(config.Acme, cert, cancellationToken);
                            switch (result)
                            {
                                case RenewalResult.NoChange:
                                    log.LogInformation($"Certificate renewal skipped for: {hostNames} (no change required yet)");
                                    break;
                                case RenewalResult.Success:
                                    log.LogInformation($"Certificate renewal succeeded for: {hostNames}");
                                    break;
                                default:
                                    throw new ArgumentOutOfRangeException(result.ToString());
                            }
                        }
                        catch (Exception e)
                        {
                            log.LogError(e, $"Certificate renewal failed for: {hostNames}!");
                        }
                    }
                }
            }
        }

        private static async Task<IEnumerable<(string configName, Configuration)>> LoadConfigFilesAsync(IStorageProvider storageProvider,
            IConfigurationProcessor processor,
            ILogger log,
            CancellationToken cancellationToken)

        {
            var configs = new List<(string, Configuration)>();
            var paths = await storageProvider.ListAsync("config/", cancellationToken);
            foreach (var path in paths)
            {
                if ("config/sample.json".Equals(path, StringComparison.OrdinalIgnoreCase))
                    continue; // ignore

                var content = await storageProvider.GetAsync(path, cancellationToken);
                try
                {
                    configs.Add((path, processor.ValidateAndLoad(content)));
                }
                catch (Exception e)
                {
                    log.LogError(e, "Failed to process configuration file " + path);
                }
            }
            if (!paths.Any())
            {
                var content = await File.ReadAllTextAsync("sample.json", cancellationToken);
                await storageProvider.SetAsync("config/sample.json", content, cancellationToken);
            }
            return configs.ToArray();
        }
    }
}
