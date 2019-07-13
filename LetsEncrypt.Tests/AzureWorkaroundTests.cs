using FluentAssertions;
using LetsEncrypt.Logic;
using LetsEncrypt.Tests.Helpers;
using NUnit.Framework;
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace LetsEncrypt.Tests
{
    public class AzureWorkaroundTests
    {
        [Test]
        public void SubscriptionIdShouldBeReadFromEnvironmentVariable()
        {
            try
            {
                const string fakeSubscriptionId = "68373267-6C36-4B66-B92F-F124A23E313E";
                Environment.SetEnvironmentVariable("subscriptionId", fakeSubscriptionId);

                var az = new AzureHelper();
                az.GetSubscriptionId().Should().Be(fakeSubscriptionId);
            }
            finally
            {
                Environment.SetEnvironmentVariable("subscriptionId", null);
            }
        }

        [Test]
        public void SubscriptionIdShouldThrowIfNotSet()
        {
            Environment.SetEnvironmentVariable("subscriptionId", null);

            var az = new AzureHelper();
            new Action(() => az.GetSubscriptionId()).Should().Throw<ArgumentException>();
        }

        [Test]
        public async Task TenantIdShouldBeReadFromAzure()
        {
            try
            {
                // setup
                const string fakeSubscriptionId = "68373267-6C36-4B66-B92F-F124A23E313E";
                const string fakeTenantId = "68373267-6C36-4B66-B92F-000000000000";
                // mock http request
                var mock = new MockHttpMessageHandler(req =>
                {
                    var url = req.RequestUri.ToString();
                    if (!url.Contains($"https://management.azure.com/subscriptions/{fakeSubscriptionId}"))
                        throw new NotSupportedException("Invalid request on mock: " + url);

                    var resp = new HttpResponseMessage(HttpStatusCode.Unauthorized);
                    resp.Headers.WwwAuthenticate.Add(new AuthenticationHeaderValue("Bearer",
                        $"authorization_uri=\"https://login.windows.net/{fakeTenantId}\", error=\"invalid_token\", error_description=\"The authentication failed because of missing 'Authorization' header.\""));
                    return resp;
                });
                var az = new AzureHelper(mock);
                Environment.SetEnvironmentVariable("subscriptionId", fakeSubscriptionId);

                // act + verify
                var t = await az.GetTenantIdAsync(CancellationToken.None);
                t.Should().Be(fakeTenantId);
            }
            finally
            {
                Environment.SetEnvironmentVariable("subscriptionId", null);
            }
        }
    }
}
