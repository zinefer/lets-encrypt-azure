using Certes;
using System;

namespace LetsEncrypt.Logic.Acme
{
    /// <summary>
    /// Abstraction to make the rest of the code testable without connecting to Let's Encrypt every time
    /// </summary>
    public interface IAcmeContextFactory
    {
        IAcmeContext GetContext(Uri certificateAuthorityUri, IKey existingKey = null);
    }
}
