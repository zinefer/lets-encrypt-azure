# Setup

For a basic tutorial about deployments with YAML pipelines in Azure DevOps and azure function deployment see my [blog post](https://marcstan.net/blog/2019/12/07/Using-Azure-DevOps-to-deploy-Azure-functions-from-Github/).

You must perform these steps to getting this solution up and running:

## Prerequisites

A keyvault to store the certificates. If you need multiple certificates it is up to you whether you want to use a central keyvault or multiple. Note that so far all supported resources (cdn, app service) require the keyvault to be in the same subscription (but not necessarily the same resourcegroup).

Personally I use one keyvault per project to have a clean seperation betweem projects but a central "certificate keyvault" might make sense as well.

Please see the individual `targetResources` in [Supportes resources](./Supported%20resources.md#targetResource) for additional prerequisites based on whichever resources will consume the certificate.

## Setting up the deployment

The [azure-pipelines.yml](./azure-pipelines.yml) contains the full infrastructure and code deployment (you can ignore the `integrationtests.yml` as I use those for verification during development).

The pipeline can run on any Microsoft Hosted agent as all the software it needs is installed on those (Visual Studio, .Net Core, Az module, ..).

Before running the pipeline you must modify it a bit:

Get rid of this group definition:
``` yaml
- group: 'Deployment Credentials'
```

The resourcegroup name used in Azure is also used for storage account, app insights and the function app and must thus be globally unique, so pick something unique (with less than 23 characters to avoid hitting Azure limits):

``` yaml
- name: ResourceGroupName
  value: 'letsencrypt-func'
```

The service connection which is used to deploy your code (see service connection tab in project settings for the exact name you have configured). This is defined multiple times in the yaml file as it cannot be templated:

``` yaml
azureSubscription: 'Opensource Deployments'
```

Optionally get rid of the `schedules` section at the top (I like my pipelines to run periodically so I know when something is broken).

The modified pipeline should now execute successfully in your account.

Alternatively you can execute the same steps manually from your machine using Az powershell module and Visual Studio.

## Configure

Along with the function app a storage account is created and the keyvault(s) previously mentioned must exist.

The storage account is used to store the metadata and configuration files for the renewal process (inside container `letsencrypt`).

## First run

If the function was deployed successfully you can run it once manually (using the provided http function `execute` **Note** that it only accepts POST requests with an empty body).

The storage account should then contain a container `letsencrypt` with a `config/sample.json` file (both will be automatically created by the function call if they don't exist yet).

The file contains comments and you should be able to add your own web apps and domains quite easily based on them.

**Note:** The `config/sample.json` file is always ignored when generating certificates, you must use a different filename (e.g. config/my-domain.json).

(Personal recommendation: If you name Azure resources, name them all identical, e.g. resourcegroup "letsencrypt-func" -> azure function "letsencrypt-func", keyvault -> "letsencrypt-func", etc. The configuration has a fallback system, if most resources are named identical you only need to specify one and the rest are inferred by the config fallback system).

See [Supported resources](./Supported%20resources.md) for more documentation including the required permissions.

## Deploy first certificate

If Azure CDN is setup correctly (with the endpoint and domain already correctly mapped) and the permissions are assigned correctly then the function should create certificates in the keyvault after a successful run and attach them to the CDN/update them when needed.

**Note:** The CDN update can take up to 6 hours until the certificate is used and you can see the progress in the CDN -> Endpoints section in Azure (the function will not check for it once it has triggered the deploy successfully).

## Known issues

* 400 Bad Request when updating the CDN - this will happen when a CDN certificate update is already in progress (even if the request is for the same certificate). Since the process takes up to 6 hours, check back later when the CDN Endpoint is no longer provisioning

# Invoking the function manually

By default the function is schedule to run daily and only updates the certificates if they are older than X days (default: 30 days).

To manually invoke it, call the endpoint `<your-function>.azurewebsites.net/api/execute`. Note that it is a POST endpoint.

It currently requires no body but allows overrides via querystring:

* `newCertificate` - if set to `true` will issue new Let's Encrypt certificates for all sites even if the existing certificates haven't expired (this will also update the azure resources with the new certificates)