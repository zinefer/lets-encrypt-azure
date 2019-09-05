namespace LetsEncrypt.Func
{
    public static class Schedule
    {
        /// <summary>
        /// Run daily but not at midnight to prevent service DoS as per letsencrypt guidelines.
        /// </summary>
        public const string Daily = "0 51 23 * * *";

        /// <summary>
        /// Run twice daily and slightly after the initial cert deployment.
        /// Used for integration tests.
        /// </summary>
        public const string TwiceDaily = "0 15 0,12 * * *";
    }
}
