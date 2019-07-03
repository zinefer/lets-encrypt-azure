using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Rest;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace LetsEncrypt.Logic
{
    public class MsiTokenProvider : ServiceClientCredentials
    {
        private readonly AzureServiceTokenProvider _tokenProvider;
        private readonly Func<HttpRequestMessage, string> _resourceProvider;

        public MsiTokenProvider(AzureServiceTokenProvider tokenProvider, Func<HttpRequestMessage, string> resourceProvider)
        {
            _tokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));
            _resourceProvider = resourceProvider ?? throw new ArgumentNullException(nameof(resourceProvider));
        }

        public override async Task ProcessHttpRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var auth = await _tokenProvider.GetAuthenticationResultAsync(_resourceProvider(request));
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth.AccessToken);

            await base.ProcessHttpRequestAsync(request, cancellationToken);
        }
    }
}
