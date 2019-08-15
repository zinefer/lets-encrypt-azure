# Azure function based Let's Encrypt automation

Automatically issue Let's Encrypt SSL certificates for all your custom domain names in Azure.

[Go to release](https://dev.azure.com/marcstanlive/Opensource/_build/definition?definitionId=31) 

![LetsEncrypt.Azure](https://dev.azure.com/marcstanlive/Opensource/_apis/build/status/31)

# Motivation

Existing solutions ([Let's Encrypt Site Extension](https://github.com/sjkp/letsencrypt-siteextension), [Let's Encrypt Webapp Renewer](https://github.com/ohadschn/letsencrypt-webapp-renewer)) work well but are target at Azure App Services only.

This solution also enables Azure CDN based domains to use Let's Encrypt certificates (Azure CDN is needed if you want a custom domain name for your static website hosted via azure blob storage).

If you want to know how to setup an Azure CDN based website backed by Blob Storage, [read my blog post](https://marcstan.net/blog/2019/07/12/Static-websites-via-Azure-Storage-and-CDN/).


# Features

* automated Let's Encrypt certificate renewal for
    * Azure App Service
    * Azure CDN
* securely store certificates in keyvaults
* cheap to run (< 0.05$/month)

# Setup

You must perform these steps to getting this solution up and running:

## Prerequisites

A keyvault to store the certificates. If you need multiple certificates it is up to you whether you want to use a central keyvault or multiple. Note that so far all supported resources (cdn, app service) require the keyvault to be in the same subscription (but not necessarily the same resourcegroup).

Personally I use one keyvault per project to have a clean seperation betweem projects but a central "certificate keyvault" might make sense as well.

Please see the individual `targetResources` in [Supportes resources](./Supported%20resources.md#targetResource) for additional prerequisites based on whichever resources will consume the certificate.

## Deploy

The [azure-pipelines.yml](./azure-pipelines.yml) contains the full infrastructure and code deployment, all you need to do is modify the variables (custom resourcegroup name) and run it.

Alternatively you can execute the same steps manually from your machine using Az powershell module and Visual Studio.

## Configure

Along with the function app a storage account is created and the keyvault(s) previously mentioned must exist.

The storage account is used to store the metadata and configuration files for the renewal process (inside container `letsencrypt`).

### First run

If the function was deployed successfully you can run it once manually (using the provided http function `execute` **Note** that it only accepts POST requests with an empty body).

The storage account should then contain a container `letsencrypt` with a `config/sample.json` file (both will be automatically created by the function call if they don't exist yet).

The file contains comments and you should be able to add your own web apps and domains quite easily based on them.

**Note:** The `config/sample.json` file is always ignored when generating certificates, you must use a different filename (e.g. config/my-domain.json).

(Personal recommendation: If you name Azure resources, name them all identical, e.g. resourcegroup "letsencrypt-func" -> azure function "letsencrypt-func", keyvault -> "letsencrypt-func", etc. The configuration has a fallback system, if most resources are named identical you only need to specify one and the rest are inferred by the config fallback system).

See [Supported resources](./Supported%20resources.md) for more documentation including the required permissions.

## Deploy first certificate

If Azure CDN is setup correctly (with the endpoint and domain already correctly mapped) and the permissions are assigned correctly then the function should create certificates in the keyvault after a successful run and attach them to the CDN/update them when needed.

**Note:** The CDN update can take up to 6 hours until the certificate is used and you can see the progress in the CDN -> Endpoints section (the function will not check for it once it has triggered the deploy successfully).

## Known issues

* 400 Bad Request when updating the CDN - this will happen when a CDN certificate update is already in progress (even if the request is for the same certificate). Since the process takes up to 6 hours, check back later when the CDN Endpoint is no longer provisioning

# Invoking the function manually

By default the function is schedule to run daily and only updates the certificates if they are older than X days (default: 30 days).

To manually invoke it, call the endpoint `<your-function>.azurewebsites.net/api/execute`. Note that it is a POST endpoint.

It currently requires no body but allows a few overrides via querystring:

* `newCertificate` - if set to `true` will issue new Let's Encrypt certificates for all sites even if the existing certificates haven't expired (this will also update the azure resources with the new certificates)
* `updateResource` - if set to `true` will update the azure resource with the existing certificate. This is useful if you already have a certificate in the keyvault and just want to update the azure resources to use it, without issuing a new certificate