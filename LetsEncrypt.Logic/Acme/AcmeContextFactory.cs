using Certes;
using System;

namespace LetsEncrypt.Logic.Acme
{
    public class AcmeContextFactory : IAcmeContextFactory
    {
        public IAcmeContext GetContext(Uri certificateAuthorityUri, IKey existingKey = null)
        {
            return new AcmeContext(certificateAuthorityUri, existingKey);
        }
    }
}
