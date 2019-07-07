# Azure function based Let's Encrypt automation (for Azure CDN)

Automatically issue SSL certificates for all your custom domain names assigned to Azure CDN.

:warning: **Only supports Azure CDN right now**

If you want to renew certificates for app services look no further than the [webapp renewer webjob](https://github.com/ohadschn/letsencrypt-webapp-renewer) (which I [modified](https://github.com/MarcStan/letsencrypt-webapp-renewer) to suit my workflow).

# Motivation

Existing solutions ([Let's Encrypt Site Extension](https://github.com/sjkp/letsencrypt-siteextension), [Let's Encrypt Webapp Renewer](https://github.com/ohadschn/letsencrypt-webapp-renewer)) work well but are target at Azure App Services.

This solution enables Azure CDN based domains to use LetsEncrypt certificates (Azure CDN is needed if you want a custom domain name for your static website hosted via azure blob storage).

# Installation

There are 3 parts to getting this solution up and running:

## Deploy

Prerequesite: A keyvault to store the certificates. If you need multiple certificates it is up to you whether you want to use a central keyvault or multiple. Personally I use one keyvault per project to have a clean seperation.

(Personal recommendation: If you name Azure resources, name them all identical, e.g. resourcegroup "letsencrypt-func" -> azure function "letsencrypt-func", keyvault -> "letsencrypt-func", etc. The configuration has a fallback system, if most resources are named identical you only need to specify one and the rest are inferred).

As per the CDN documentation:

```
You need to setup the right permissions for CDN to access your Key vault:
1) Register Azure CDN as an app in your Azure Active Directory (AAD) via PowerShell using this command: New-AzureRmADServicePrincipal -ApplicationId "205478c0-bd83-4e1b-a9d6-db63a3e1e1c8".
2) Grant Azure CDN service the permission to access the secrets in your Key vault. Go to “Access policies” from your Key vault to add a new policy, then grant “Microsoft.Azure.Cdn” service principal a “get-secret” permission.
```

The [azure-pipelines.yaml](./azure-pipelines.yaml) contains the full infrastructure and code deployment, all you need to do is modify the variables (custom resourcegroup name) and run it.

Alternatively you can execute the same steps manually from your machine using Az powershell module and Visual Studio.

## Configure

Along with the function app a storage account is created and the keyvault previously mentioned must exist.

They are used to store the metadata and certificates for the renewal process.

If the function was deployed successfully you can run it once manually. The storage account should then contain a container `letsencrypt` with a `config/sample.json` file (both will be automatically created by the function call if they don't exist yet).

The file contains comments and you should be able to add your own web apps and domains quite easily based on them.

**Note:** The config/sample.json file is always ignored when generating certificates, you must use a different filename (e.g. config/my-domain.json).

The azure function will have it's [managed identity](https://docs.microsoft.com/azure/active-directory/managed-identities-azure-resources/overview) enabled (service principal name will be the same as the function instance name). You need to grant it at least Get, List, Update and Import permissions for `Certificates` in all keyvaults that you intend to use.

## Run

For the function to succeed a few conditions must be met:

1. Azure CDN must be setup correctly (with the endpoint and domain correctly mapped)
2. The function must have access to write a certificate to the keyvault
3. The function must have access to read/write the CDN (read to fetch endpoints, write to update the certificate)

If everything is setup correctly the function should create certificates in the keyvault after a successful run and attach them to the CDN/update them when needed.

Note that the CDN update can take up to 6 hours until the certificate is used and you can see the progress in the CDN -> Endpoints section.
