namespace LetsEncrypt.Logic.Config
{
    /// <summary>
    /// Options related to a single certificate.
    /// Which hostnames, how to validate and where to store and apply to.
    /// </summary>
    public class CertificateRenewalOptions
    {
        /// <summary>
        /// The set of hostnames for which to issue a certificate.
        /// Note that these will all be issued into a single certificate.
        /// </summary>
        public string[] HostNames { get; set; }

        /// <summary>
        /// Config section related to the challenge that must be solved to prove ownership of a domain.
        /// </summary>
        public GenericEntry ChallengeResponder { get; set; }

        /// <summary>
        /// Target store where the certificate will be saved on success.
        /// </summary>
        public GenericEntry CertificateStore { get; set; }

        /// <summary>
        /// The resource where the certificate should be applied to once it is stored.
        /// </summary>
        public GenericEntry TargetResource { get; set; }
    }
}
