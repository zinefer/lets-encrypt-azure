namespace LetsEncrypt.Logic.Azure.Response
{
    public class CertificateResponse
    {
        public string Name { get; set; }

        public string Thumbprint { get; set; }

        public string[] HostNames { get; set; }
    }
}
