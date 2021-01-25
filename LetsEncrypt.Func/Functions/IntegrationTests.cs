using LetsEncrypt.Func.Config;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using ExecutionContext = Microsoft.Azure.WebJobs.ExecutionContext;

namespace LetsEncrypt.Func.Functions
{
    public class IntegrationTests
    {
        private readonly IConfigurationLoader _configurationLoader;
        private readonly ILogger _logger;

        public IntegrationTests(
            IConfigurationLoader configurationLoader,
            ILogger<IntegrationTests> logger)
        {
            _configurationLoader = configurationLoader;
            _logger = logger;
        }

        /// <summary>
        /// Time triggered function that checks every domain for a valid https certificate and logs exceptions if not.
        /// </summary>
        /// <param name="timer"></param>
        /// <param name="cancellationToken"></param>
        [FunctionName("integration-test")]
        public Task RenewAsync(
          [TimerTrigger(Schedule.TwiceDaily)] TimerInfo timer,
          ExecutionContext executionContext,
          CancellationToken cancellationToken)
            => CheckDomainsForValidCertificateAsync(executionContext, cancellationToken);

        private async Task CheckDomainsForValidCertificateAsync(
            ExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            var configurations = await _configurationLoader.LoadConfigFilesAsync(executionContext, cancellationToken);
            var errors = new List<Exception>();
            var httpClient = new HttpClient();
            foreach ((var name, var config) in configurations)
            {
                using (_logger.BeginScope($"Checking certificates from {name}"))
                {
                    foreach (var cert in config.Certificates)
                    {
                        var hostNames = string.Join(";", cert.HostNames);
                        try
                        {
                            // check each domain to verify HTTPS certificate is valid
                            var request = WebRequest.CreateHttp($"https://{cert.HostNames.First()}");
                            request.ServerCertificateValidationCallback += ValidateTestServerCertificate;
                            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse()) { }
                        }
                        catch (Exception e)
                        {
                            _logger.LogError(e, $"Certificate check failed for: {hostNames}!");
                            errors.Add(e);
                            continue;
                        }
                        _logger.LogInformation($"Certificate for {hostNames} looks valid");
                    }
                }
            }
            if (!configurations.Any())
            {
                _logger.LogWarning("No configurations where processed, refere to the sample on how to set up configs!");
            }
            if (errors.Any())
                throw new AggregateException("Failed to process all certificates", errors);
        }

        private static bool ValidateTestServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            // https://stackoverflow.com/a/22106658

            // If the certificate is a valid, signed certificate, return true.
            if (sslPolicyErrors == SslPolicyErrors.None)
            {
                return true;
            }

            // If there are errors in the certificate chain, look at each error to determine the cause.
            if ((sslPolicyErrors & SslPolicyErrors.RemoteCertificateChainErrors) != 0)
            {
                if (chain != null && chain.ChainStatus != null)
                {
                    foreach (X509ChainStatus status in chain.ChainStatus)
                    {
                        if ((certificate.Subject == certificate.Issuer) &&
                           (status.Status == X509ChainStatusFlags.UntrustedRoot))
                        {
                            // Self-signed certificates with an untrusted root are valid. 
                            continue;
                        }
                        else
                        {
                            if (status.Status != X509ChainStatusFlags.NoError)
                            {
                                // If there are any other errors in the certificate chain, the certificate is invalid,
                                // so the method returns false.
                                return false;
                            }
                        }
                    }
                }

                // When processing reaches this line, the only errors in the certificate chain are 
                // untrusted root errors for self-signed certificates. These certificates are valid
                // for default Exchange server installations, so return true.
                return true;
            }
            return false;
        }
    }
}
