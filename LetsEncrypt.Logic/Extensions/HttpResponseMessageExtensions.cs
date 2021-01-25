using Azure;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace LetsEncrypt.Logic.Extensions
{
    public static class HttpResponseMessageExtensions
    {
        /// <summary>
        /// Helper that reads the response and adds it to the exception if the response is not successful.
        /// </summary>
        /// <param name="response"></param>
        /// <param name="errorMessage"></param>
        public static async Task EnsureSuccessAsync(this HttpResponseMessage response, string errorMessage)
        {
            if (!response.IsSuccessStatusCode)
            {
                var c = await response.Content.ReadAsStringAsync();
                throw new RequestFailedException((int)response.StatusCode, $"{errorMessage}. See inner exception for details.", new InvalidOperationException(c));
            }
        }
    }
}
