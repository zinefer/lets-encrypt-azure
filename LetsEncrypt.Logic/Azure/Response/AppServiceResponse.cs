namespace LetsEncrypt.Logic.Azure.Response
{
    public class AppServiceResponse
    {
        public string Location { get; set; }

        public string ServerFarmId { get; set; }

        public AppServiceCustomDomain[] CustomDomains { get; set; }
    }

    public class AppServiceCustomDomain
    {
        public string HostName { get; set; }

        public string Thumbprint { get; set; }
    }
}
