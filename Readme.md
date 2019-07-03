# Azure function based Let's Encrypt automation (for Azure CDN)

Automatically issue SSL certificates for all your custom domain names assigned to Azure CDN.

:warning: **Only supports Azure CDN right now**

If you want to renew certificates for app services look no further than the [webapp renewer webjob](https://github.com/ohadschn/letsencrypt-webapp-renewer) (which I [modified](https://github.com/MarcStan/letsencrypt-webapp-renewer) to suit my workflow).

# Motivation

Existing solutions ([Let's Encrypt Site Extension](https://github.com/sjkp/letsencrypt-siteextension), [Let's Encrypt Webapp Renewer](https://github.com/ohadschn/letsencrypt-webapp-renewer)) work well but are target at Azure App Services.

This solution enables Azure CDN based domains to use LetsEncrypt certificates (Azure CDN is needed if you want a custom domain name for your storage ).

Currently it 

# Installation

There are 3 parts to getting this solution up and running

## Deploy

Deploying this solution can be done by first calling deploy\Deploy.ps1 with the required parameters to create the infrastructure.

You can chose to call it from your local machine (assuming you have sufficient rights in the subscription) or automate it via a free [Azure DevOps](https://azure.microsoft.com/services/devops/) pipeline.

The [azure-pipelines.yaml](./azure-pipelines.yaml) contains the full infrastructure and code deployment, all you need to do is modify the variables (custom resourcegroup name, )

Once the infrastructure exists you can deploy the function app (directly from Visual Studio or have the pipeline do it).

## Configure

Along with the function app a storage account is created and a keyvault must exist.

They are used to store the metadata and certificates for the renewal process.

If the function was deployed successfully it will [run once automatically](https://docs.microsoft.com/en-us/Azure/azure-functions/functions-manually-run-non-http). The storage account should then contain a container `letsencrypt` with a `config.json` file (both will be automatically created by the function call if they don't exist yet).

The file contains comments and you should be able to add your own web apps and domains quite easily based on them, here's an additional example that will issue a certificate for a webapp and its slot respectively:

``` json
// TODO: specify format
```

## Run

For the function to succeed a few conditions must be met:

1. Azure CDN must be setup correctly (with the endpoint and domain correctly mapped)
2. The function must have access to write a certificate to the keyvault
3. The function must have access to read/write the CDN (read to fetch endpoints, write to update the certificate)

If everything is setup correctly the function should create certificates in the keyvault after a successful run and attach them to the CDN/update them when needed.

Note that initial certificate provisioning can take a long time for CDN (6+ hours) you can see the progress in the CDN -> Endpoints section.