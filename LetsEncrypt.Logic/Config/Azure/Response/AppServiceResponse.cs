namespace LetsEncrypt.Logic.Azure.Response
{
    public class AppServiceResponse
    {
        public string Location { get; set; }

        public string ServerFarmId { get; set; }

        public string[] Hostnames { get; set; }
    }
}
