namespace LetsEncrypt.Logic.Config
{
    public class Overrides
    {
        public static Overrides None { get; } = new Overrides();

        /// <summary>
        /// If set all certificates are renewed even if they are up to date.
        /// Respects <see cref="DomainsToUpdate"/> if set.
        /// </summary>
        public bool ForceNewCertificates { get; set; }

        /// <summary>
        /// Optional array.
        /// If set then only certificates that contain at least one of the listed domains are renewed.
        /// This allows the user to force renewal of a subset of certificates if need be.
        /// </summary>
        public string[] DomainsToUpdate { get; set; } = new string[0];
    }
}
