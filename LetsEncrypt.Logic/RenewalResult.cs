namespace LetsEncrypt.Logic
{
    public enum RenewalResult
    {
        /// <summary>
        /// The certificate was not renewed because the existing certificate is still valid for long enough.
        /// </summary>
        NoChange = 0,

        /// <summary>
        /// The certificate was successfully renewed.
        /// </summary>
        Success = 1
    }
}
