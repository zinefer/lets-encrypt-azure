using System;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace LetsEncrypt.Logic
{
    /// <summary>
    /// Helpers to get well known ids from azure.
    /// </summary>
    public class AzureWorkarounds
    {
        private readonly HttpClient _httpClient;

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        /// <param name="handler">Used for testing overrides</param>
        public AzureWorkarounds(HttpMessageHandler handler = null)
        {
            _httpClient = new HttpClient(handler ?? new HttpClientHandler());
        }

        public string GetSubscriptionId()
        {
            var s = Environment.GetEnvironmentVariable("subscriptionId");
            if (string.IsNullOrEmpty(s))
                throw new ArgumentException("Environment variable 'subscriptionId' must be set to the current subscription. Use local.settings.json for local testing or set app setting via release pipeline.");
            return s;
        }

        /// <summary>
        /// Returns the tenant id by making an unauthorized call to azure.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<string> GetTenantIdAsync(CancellationToken cancellationToken)
        {
            // unauthorized call yields header with tenant id
            // works because every subscription is only ever tied to one tenant (and MSI auth is limited to said tenant)
            var url = $"https://management.azure.com/subscriptions/{GetSubscriptionId()}?api-version=2015-01-01";
            var response = await _httpClient.GetAsync(url, cancellationToken);
            var header = response.Headers.WwwAuthenticate.FirstOrDefault();
            var regex = new Regex("authorization_uri=\"https:\\/\\/login\\.windows\\.net\\/([A-Za-z0-9-]*)\"");
            var match = regex.Match(header.Parameter);
            if (!match.Success)
                throw new NotSupportedException("Azure endpoint failed to return the tenantId!");

            return match.Groups[1].Value;
        }
    }
}
