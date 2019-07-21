namespace LetsEncrypt.Logic.Config
{
    public class Overrides
    {
        public static Overrides None { get; } = new Overrides();

        public bool NewCertificate { get; set; }

        public bool UpdateResource { get; set; }
    }
}
