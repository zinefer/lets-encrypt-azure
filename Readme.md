# Azure function based Let's Encrypt automation

Automatically issue Let's Encrypt SSL certificates for all your custom domain names in Azure.

[![LetsEncrypt.Azure](https://dev.azure.com/marcstanlive/Opensource/_apis/build/status/31)](https://dev.azure.com/marcstanlive/Opensource/_build/definition?definitionId=31)

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

See [Setup](./docs/Setup.md).

# Changelog

Changelog is [here](Changelog.md).