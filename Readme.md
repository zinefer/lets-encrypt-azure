**This is currently under development and barely tested. Use at your own risk!**
___

# Azure function based Let's Encrypt automation (Azure CDN only)

Automatically issue Let's Encrypt SSL certificates for all your custom domain names in Azure.

[Go to release](https://dev.azure.com/marcstanlive/Opensource/_build/definition?definitionId=31) 

![LetsEncrypt.Azure](https://dev.azure.com/marcstanlive/Opensource/_apis/build/status/31)

Usecase: Using a custom domain (especially root domain) is a bit tricky with Azure Storage (Static Websites) + Azure CDN right now. This aims to automate the certificate part.

If you want to know how to setup an Azure CDN based website backed by Blob Storage, [read my blog post](https://marcstan.net/blog/2019/07/12/Static-websites-via-Azure-Storage-and-CDN/).

:warning: **Only supports Azure CDN backed by Blob Storage right now**

If you want to renew certificates for app services look no further than the [webapp renewer webjob](https://github.com/ohadschn/letsencrypt-webapp-renewer) (which I [modified](https://github.com/MarcStan/letsencrypt-webapp-renewer) to suit my workflow).

# Motivation

Existing solutions ([Let's Encrypt Site Extension](https://github.com/sjkp/letsencrypt-siteextension), [Let's Encrypt Webapp Renewer](https://github.com/ohadschn/letsencrypt-webapp-renewer)) work well but are target at Azure App Services only.

This solution enables Azure CDN based domains to use Let's Encrypt certificates (Azure CDN is needed if you want a custom domain name for your static website hosted via azure blob storage).

# Installation

You must perform these steps to getting this solution up and running:

## Prerequesites

A keyvault to store the certificates. If you need multiple certificates it is up to you whether you want to use a central keyvault or multiple. Personally I use one keyvault per project to have a clean seperation betweem them but a central "Let's Encrypt certificate" keyvault might make sense as well.

As per the CDN documentation (you can also find this documentation in the custom domain settings of Azure CDN endpoints):

```
You need to setup the right permissions for CDN to access your Key vault:
1) Register Azure CDN as an app in your Azure Active Directory (AAD) via PowerShell using this command: New-AzureRmADServicePrincipal -ApplicationId "205478c0-bd83-4e1b-a9d6-db63a3e1e1c8".
2) Grant Azure CDN service the permission to access the secrets in your Key vault. Go to “Access policies” from your Key vault to add a new policy, then grant “Microsoft.Azure.Cdn” service principal a “get-secret” permission.
```

## Deploy

The [azure-pipelines.yaml](./azure-pipelines.yaml) contains the full infrastructure and code deployment, all you need to do is modify the variables (custom resourcegroup name) and run it.

Alternatively you can execute the same steps manually from your machine using Az powershell module and Visual Studio.

## Configure

Along with the function app a storage account is created and the keyvault(s) previously mentioned must exist.

The storage account is used to store the metadata and configuration for the renewal process (inside container `letsencrypt`).

If the function was deployed successfully you can run it once manually (using the provided http function `execute`). The storage account should then contain a container `letsencrypt` with a `config/sample.json` file (both will be automatically created by the function call if they don't exist yet).

The file contains comments and you should be able to add your own web apps and domains quite easily based on them.

**Note:** The `config/sample.json` file is always ignored when generating certificates, you must use a different filename (e.g. config/my-domain.json).

(Personal recommendation: If you name Azure resources, name them all identical, e.g. resourcegroup "letsencrypt-func" -> azure function "letsencrypt-func", keyvault -> "letsencrypt-func", etc. The configuration has a fallback system, if most resources are named identical you only need to specify one and the rest are inferred by the config fallback system).

## Permissions

The azure function will have it's [managed identity](https://docs.microsoft.com/azure/active-directory/managed-identities-azure-resources/overview) enabled (service principal name will be the same as the function instance name).

You need to grant it at least Get, List, Update and Import permissions for `Certificates` in all keyvaults that you intend to use for certificate storage.

Additionally the function needs to be `CDN Endpoint Contributor` on every CDN endpoint.

If you opted for the (recommended) MSI based storage access, then the function MSI must also be `Storage Blob Data Contributor` on every `$web` container used by the Azure CDN (as it must upload and delete the Let's Encrypt http challenge files).

As mentioned before, the `Microsoft.Azure.Cdn` app must be registered in the subscription and must have get secret permissions on every keyvault that will contain Let's Encrypt certificates.

Note that RBAC changes may take up to 5 minutes to reflect.

## Run

If Azure CDN is setup correctly (with the endpoint and domain already correctly mapped) and the permissions are assigned correctly then the function should create certificates in the keyvault after a successful run and attach them to the CDN/update them when needed.

**Note:** The CDN update can take up to 6 hours until the certificate is used and you can see the progress in the CDN -> Endpoints section (the function will not check for it once it has triggered the deploy successfully).
