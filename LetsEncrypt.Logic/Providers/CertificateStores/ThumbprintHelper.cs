using System;

namespace LetsEncrypt.Logic.Providers.CertificateStores
{
    public static class ThumbprintHelper
    {
        public static string Convert(byte[] thumbprint) => BitConverter.ToString(thumbprint).Replace("-", "");
    }
}
