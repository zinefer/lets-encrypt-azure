using Certes;
using Certes.Acme;
using LetsEncrypt.Logic.Config;
using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace LetsEncrypt.Logic.Acme
{
    public class CertificateBuilder : ICertificateBuilder
    {
        private static readonly RNGCryptoServiceProvider _randomGenerator = new RNGCryptoServiceProvider();

        public async Task<(byte[] pfxBytes, string password)> BuildCertificateAsync(
            IOrderContext order,
            CertificateRenewalOptions cfg,
            CancellationToken cancellationToken)
        {
            var key = KeyFactory.NewKey(KeyAlgorithm.RS256);
            await order.Finalize(new CsrInfo(), key);

            var certChain = await order.Download();
            var builder = certChain.ToPfx(key);
            builder.FullChain = true;

            var bytes = new byte[32];
            _randomGenerator.GetNonZeroBytes(bytes);
            var password = Convert.ToBase64String(bytes);
            var pfxBytes = builder.Build(string.Join(";", cfg.HostNames), password);
            return (pfxBytes, password);
        }
    }
}
