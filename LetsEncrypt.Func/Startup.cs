using Azure.Core;
using Azure.Identity;
using LetsEncrypt.Func;
using LetsEncrypt.Logic;
using LetsEncrypt.Logic.Storage;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

[assembly: FunctionsStartup(typeof(Startup))]
namespace LetsEncrypt.Func
{
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            var configuration = builder.GetContext().Configuration;

            // internal storage (used for letsencrypt account metadata)
            builder.Services.AddSingleton<IStorageProvider>(new AzureBlobStorageProvider(configuration["AzureWebJobsStorage"], "letsencrypt"));

            builder.Services.AddSingleton<TokenCredential, DefaultAzureCredential>();

            builder.Services.Scan(scan =>
            scan.FromAssemblyOf<RenewalService>()
                .AddClasses()
                .AsMatchingInterface()
                .WithTransientLifetime()
                .FromAssemblyOf<Startup>()
                .AddClasses()
                .AsMatchingInterface()
                .WithTransientLifetime());
        }
    }
}
