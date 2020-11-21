using Azure.Core;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace LetsEncrypt.Logic.Authentication
{
    public class MsiTokenProvider : DelegatingHandler
    {
        private readonly TokenCredential _tokenProvider;
        private readonly string _scope;

        /// <summary>
        /// Wrapper to authenticate against a specific endpoint.
        /// </summary>
        /// <param name="tokenCredential"></param>
        public MsiTokenProvider(TokenCredential tokenCredential, string scope)
            : base(new HttpClientHandler())
        {
            _tokenProvider = tokenCredential ?? throw new ArgumentNullException(nameof(tokenCredential));
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var ctx = new TokenRequestContext(new[]
            {
                _scope
            });
            var auth = await _tokenProvider.GetTokenAsync(ctx, cancellationToken);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
            return await base.SendAsync(request, cancellationToken);
        }
    }
}
