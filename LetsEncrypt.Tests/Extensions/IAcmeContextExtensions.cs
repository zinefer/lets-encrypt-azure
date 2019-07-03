using Certes;
using LetsEncrypt.Logic.Acme;
using Moq;
using System;

namespace LetsEncrypt.Tests.Extensions
{
    public static class IAcmeContextExtensions
    {
        /// <summary>
        /// Creates a factory for testing that will always return the specific context.
        /// </summary>
        /// <param name="acmeContext"></param>
        /// <returns></returns>
        public static Mock<IAcmeContextFactory> CreateFactoryMock(this IAcmeContext acmeContext)
        {
            var contextFactory = new Mock<IAcmeContextFactory>();
            contextFactory.Setup(x => x.GetContext(It.IsAny<Uri>(), It.IsAny<IKey>()))
                .Returns(acmeContext);

            return contextFactory;
        }
    }
}
