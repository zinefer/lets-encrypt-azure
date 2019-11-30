using LetsEncrypt.Logic.Authentication;
using Microsoft.Azure.Services.AppAuthentication;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace LetsEncrypt.Logic.Azure
{
    /// <summary>
    /// Helpers to get well known ids from azure.
    /// </summary>
    public class AzureHelper : IAzureHelper
    {
        private readonly HttpClient _httpClient;
        private ConcurrentDictionary<string, string> _tenantIdLookup = new ConcurrentDictionary<string, string>();
        private HttpClient _armHttpClient;

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        /// <param name="handler">Used for testing overrides</param>
        public AzureHelper(HttpMessageHandler handler = null)
        {
            _httpClient = new HttpClient(handler ?? new HttpClientHandler());
        }

        public string GetSubscriptionId()
        {
            var s = Environment.GetEnvironmentVariable("subscriptionId");
            if (string.IsNullOrEmpty(s))
                throw new ArgumentException("Environment variable 'subscriptionId' must be set to the current subscription. Use local.settings.json for local testing or set via ARM template azure function environment variable.");
            return s;
        }

        /// <summary>
        /// Returns the tenant id by making an unauthorized call to azure.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<string> GetTenantIdAsync(CancellationToken cancellationToken)
        {
            var subscriptionId = GetSubscriptionId();
            if (_tenantIdLookup.ContainsKey(subscriptionId))
                return _tenantIdLookup[subscriptionId];

            // unauthorized call yields header with tenant id
            // works because every subscription is only ever tied to one tenant (and MSI auth is limited to said tenant)
            // could have also done the same as with subscriptionId, but that way user must only set 1 value
            var url = $"https://management.azure.com/subscriptions/{subscriptionId}?api-version=2015-01-01";
            var response = await _httpClient.GetAsync(url, cancellationToken);
            var header = response.Headers.WwwAuthenticate.FirstOrDefault();
            var regex = new Regex("authorization_uri=\"https:\\/\\/login\\.windows\\.net\\/([A-Za-z0-9-]*)\"");
            var match = regex.Match(header.Parameter);
            if (!match.Success)
                throw new NotSupportedException("Azure endpoint failed to return the tenantId!");

            var tenantId = match.Groups[1].Value;
            _tenantIdLookup.AddOrUpdate(subscriptionId, tenantId, (key, old) => tenantId);
            return tenantId;
        }

        public async Task<HttpClient> GetAuthenticatedARMClientAsync(CancellationToken cancellationToken)
        {
            // don't care about race condition. worst case multiple clients are created
            if (_armHttpClient != null)
                return _armHttpClient;

            var tokenProvider = new AzureServiceTokenProvider();
            var tenantId = await GetTenantIdAsync(cancellationToken);
            // only allow connections to management API with this provider
            const string msiTokenprovider = "https://management.azure.com/";
            var cred = new MsiTokenProvider(tokenProvider, tenantId,
                req => req.RequestUri.ToString().StartsWith(msiTokenprovider)
                    ? msiTokenprovider
                    : throw new InvalidOperationException($"Token issuer was asked for a token for '{req.RequestUri}' but is only allowed to issue tokens for '{msiTokenprovider}'"));

            return _armHttpClient = new HttpClient(cred);
        }
    }
}
