using System;

namespace LetsEncrypt.Logic.Providers.CertificateStores
{
    public interface ICertificate
    {
        DateTimeOffset? NotBefore { get; }

        DateTimeOffset? Expires { get; }

        string Name { get; }

        string[] HostNames { get; }

        ICertificateStore Store { get; }

        string Thumbprint { get; }

        string Version { get; }
    }
}
