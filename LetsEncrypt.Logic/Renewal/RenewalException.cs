using Certes.Acme;
using System;

namespace LetsEncrypt.Logic.Renewal
{
    [Serializable]
    public class RenewalException : Exception
    {
        public AcmeError[] AcmeErrors { get; set; }

        public RenewalException(string message, AcmeError[] acmeErrors) : base(message)
        {
            AcmeErrors = acmeErrors ?? throw new ArgumentNullException(nameof(acmeErrors));
        }

        public RenewalException()
        {
            AcmeErrors = new AcmeError[0];
        }

        public RenewalException(string message) : base(message)
        {
            AcmeErrors = new AcmeError[0];
        }

        public RenewalException(string message, Exception innerException) : base(message, innerException)
        {
            AcmeErrors = new AcmeError[0];
        }
    }
}
