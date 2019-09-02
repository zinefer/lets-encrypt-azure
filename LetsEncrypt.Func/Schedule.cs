namespace LetsEncrypt.Func
{
    public static class Schedule
    {
        /// <summary>
        /// Run daily but not at midnight to prevent service DoS as per letsencrypt guidelines.
        /// </summary>
        public const string Daily = "0 51 11 * * *";
    }
}
