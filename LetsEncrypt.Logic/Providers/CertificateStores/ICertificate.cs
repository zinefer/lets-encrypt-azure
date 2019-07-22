using System;

namespace LetsEncrypt.Logic.Providers.CertificateStores
{
    public interface ICertificate
    {
        DateTime? NotBefore { get; }

        DateTime? Expires { get; }

        string Name { get; }

        string[] HostNames { get; }

        string Version { get; }

        ICertificateStore Store { get; }

        string Thumbprint { get; }
    }
}
