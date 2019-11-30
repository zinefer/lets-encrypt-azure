namespace LetsEncrypt.Logic.Azure.Response
{
    public class CdnResponse
    {
        public string Name { get; set; }

        public CdnCustomDomain[] CustomDomains { get; set; }
    }

    public class CdnCustomDomain
    {
        public string Name { get; set; }

        public string HostName { get; set; }
    }
}
