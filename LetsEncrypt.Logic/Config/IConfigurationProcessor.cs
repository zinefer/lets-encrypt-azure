namespace LetsEncrypt.Logic.Config
{
    public interface IConfigurationProcessor
    {
        Configuration ValidateAndLoad(string json);
    }
}
