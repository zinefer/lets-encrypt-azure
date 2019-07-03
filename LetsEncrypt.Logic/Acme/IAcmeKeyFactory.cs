using Certes;

namespace LetsEncrypt.Logic.Acme
{
    /// <summary>
    /// Abstraction to make the code testable
    /// </summary>
    public interface IAcmeKeyFactory
    {
        IKey FromPem(string pem);
    }
}
