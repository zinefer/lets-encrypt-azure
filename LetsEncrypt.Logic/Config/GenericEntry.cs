using Newtonsoft.Json.Linq;

namespace LetsEncrypt.Logic.Config
{

    public class GenericEntry
    {
        public string Type { get; set; }

        public string Name { get; set; }

        public JObject Properties { get; set; }
    }
}
