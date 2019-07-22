# Supported resources

The function runs of config files in the storage container; a short example (see [sample.json](./LetsEncrypt.Func/sample.json) for a more detailed config example):

``` json
{
  "acme": {
    "email": "you@example.com",
    "renewXDaysBeforeExpiry": 30,
    "staging": false
  },
  "certificates": [
    {
      "hostNames": [
        "example.com",
        "www.example.com"
      ],
      "challengeResponder": {
        "type": "storageAccount",
        "name": "example"
      },
      "certificateStore": {
        "type": "keyVault",
        "name": "example"
      },
      "targetResource": {
        "type": "cdn",
        "name": "example"
      }
    }
  ]
}
```

This config would renew one certificate (with two hostnames) for the cdn named "example". The certificate would be stored in keyvault "example" and the Let's Encrypt ACME challenge would be answered via the storage account "example" (using the $web container).

This is a rather compact example of the config, for a more complete example with comments and explanation of the fallback system check out the [sample config](./LetsEncrypt.Func/sample.json).

As you can see in the certificates section you can renew any number of certificates with any number of hostnames. To do so, 3 sections must be provided: `challengeResponder`, `certificateStore` and `targetResource`.

# challengeResponder

Determins how Let's Encrypt should run the ACME challenge.

Currently only `type: storageAccount` is supported and the challenge will be persisted in a storage account and then read from it by Let's Encrypt. This requires the storage (or blob) to be publicly accessible. Best practice (and the default) is to use the `$web` blob container as it is [always public anyway](https://docs.microsoft.com/en-us/azure/storage/blobs/storage-blob-static-website).

# certificateStore

Currently this is limited to Azure Keyvault. All created certificates will be stored in keyvaults as certificates.

The azure function MSI must have certificate Get, List, Import & Update permissions on the keyvault to store them.

By default the first hostname is taken as the certificate/secret name with dots substituted (example.com -> example-com).

# targetResource

This describes the Azure resource that will use the certificate. Note that each target resource will have its own way of accessing the certificates from the keyvault.

## Azure CDN (targetType=cdn)

Azure CDN will serve files from a $web container making the Let's Encrypt challenge trivial (already pointing at the correct file store).

### Permissions

The azure function will have it's [managed identity](https://docs.microsoft.com/azure/active-directory/managed-identities-azure-resources/overview) enabled (service principal name will be the same as the function instance name).

As per the [CDN documentation](https://docs.microsoft.com/en-us/azure/cdn/cdn-custom-ssl?tabs=option-2-enable-https-with-your-own-certificate#register-azure-cdn) you need to setup the right permissions for CDN to access your Key vault:

1) Register Azure CDN as an app in your Azure Active Directory (AAD) via PowerShell using this command: New-AzureRmADServicePrincipal -ApplicationId "205478c0-bd83-4e1b-a9d6-db63a3e1e1c8" (this is a tenant wide one-time action).
2) Grant Azure CDN service the permission to access the secrets in your Key vault. Go to “Access policies” from your Key vault to add a new policy, then grant “Microsoft.Azure.Cdn” service principal **secret get** permission (get secret will allow the CDN to access the certificates as each certificate has a secret alias).

Note that RBAC changes may take up to 10 minutes to update.

If you opt for the (recommended) MSI based storage access (see [sample.json](./LetsEncrypt.Func/sample.json) for the alternatives), then the function MSI must also be `Storage Blob Data Contributor` on every storage container used by the Azure CDN (defaults to `$web` container).

Contributor rights are needed as the function must upload and delete the Let's Encrypt http challenge files to issue a certificate.

Additionally the azure function MSI needs these permissions on your CDN:

Using Azure RBAC grant the azure function MSI `CDN Endpoint Contributor` permission on any endpoint that needs to be updated.

Also grant the azure function MSI `CDN Profile Reader` permission on any CDN that contains the endpoints.

(Alternatively you may also just grant these two permissions on the subscription/resourcegroup level).

Everything else is documented in detail in the [sample.json](./LetsEncrypt.Func/sample.json). You can also read [my blog post](https://marcstan.net/blog/2019/07/12/Static-websites-via-Azure-Storage-and-CDN/) on how to set up Azure CDN + Azure Storage + custom domains.

## Azure App Service (targetType=appService)

**Permissions:**

As per the [App Service documentation](https://azure.github.io/AppService/2016/05/24/Deploying-Azure-Web-App-Certificate-through-Key-Vault.html) you need to setup the right permissions for App Service to access your Key vault:

1) Register Web App as an app in your Azure Active Directory (AAD) via PowerShell using this command: New-AzureRmADServicePrincipal -ApplicationId "abfa0a7c-a6b6-4736-8310-5855508787cd" (this is a tenant wide one-time action).
2) Grant Web App service the permission to access the secrets in your Key vault. Go to “Access policies” from your Key vault to add a new policy, then grant “Microsoft.Azure.WebSites” service principal **secret get** permission (get secret will allow the App Service to access the certificates as each certificate has a secret alias).

Note that RBAC changes may take up to 10 minutes to update.

The certificate must also be transfered from storage to an azure resource. Currently it is stored next to the app service plan (name pattern: "%firstHostname%-%thumbprint%").

To ensure the azure function can store the certificate, it requires to be TODO?? `Contributor` in the resourcegroup.

Finally the azure function requires TODO?? `Contributor` permissions on each webapp as it must assign the SSL bindings from the webapp to its uploaded certificate.

**Challenge file access**

Let's Encrypt will want to access `<your domain>/.well-known/acme-challenge/*` during certificate renewal.

Since this azure function (purposefully) has no write access to any of your app services, you must use redirects and blob storage to make the challenge files accessible to Let's Encrypt.

Here's an example how it can be done with a web.config/IIS rewrite rules:
``` xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
    <system.webServer>
      <rewrite>
        <rules lockElements="clear">
          <rule name="Acme challenge" stopProcessing="true">
            <match url="^.well-known/acme-challenge/(.+)" />
            <action type="Redirect" url="https://<storageName>.<region>.web.core.windows.net/.well-known/acme-challenge/{R:1}" redirectType="Temporary" />
          </rule> 
        </rules>
      </rewrite>
    </system.webServer>
</configuration>
```

In order to use the `$web` container you must to turn on the [static website feature](https://docs.microsoft.com/en-us/azure/storage/blobs/storage-blob-static-website).

Alternatively you can use a regular container and make it publicly accessible (the URL would then be `https://<storageName>.blob.core.windows.net/<containerName>/.well-known/acme-challenge/{R:1}`), but I recommend the use of `$web` as it is very clear that its content can be accessed by anyone via the internet).

If you opt for the (recommended) MSI based storage access (see [sample.json](./LetsEncrypt.Func/sample.json) for the alternatives), then the function MSI must also be `Storage Blob Data Contributor` on every storage container used by the Azure CDN (defaults to `$web` container).

Once the redirection is enabled, any requests to your webapp hitting the `.well-known/acme-challenge/*` path should be redirected to storage.

During the Let's Encrypt challenge this will ensure that the challenge files can be upload by the azure function & read by Let's Encrypt.
