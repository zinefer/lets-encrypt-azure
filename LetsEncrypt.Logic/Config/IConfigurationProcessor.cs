namespace LetsEncrypt.Logic.Config
{
    public interface IConfigurationProcessor
    {
        /// <summary>
        /// When called will parse the provided json as a config file and return a strongly typed config object.
        /// </summary>
        /// <param name="json"></param>
        /// <returns></returns>
        Configuration ValidateAndLoad(string json);
    }
}
