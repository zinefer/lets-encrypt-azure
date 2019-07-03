using System;
using System.Collections.Generic;
using System.Text;
using Certes;

namespace LetsEncrypt.Logic.Acme
{
    public class AcmeKeyFactory : IAcmeKeyFactory
    {
        public IKey FromPem(string pem)
        {
            return KeyFactory.FromPem(pem);
        }
    }
}
