namespace LetsEncrypt.Logic.Config
{
    public class CdnDetails
    {
        public string ResourceGroupName { get; set; }

        public string CdnName { get; set; }

        public EndpointDetails[] Endpoints { get; set; }
    }
}
