# Changelog

Versioning is done by tagging commits on master. See [Versioning considerations](#Versioning-considerations) for details.

# 2.1.0

* Renew certificates automatically when domain list is changed [#19](https://github.com/MarcStan/lets-encrypt-azure/issues/19)

## api changes:

* the `newCertificate` parameter is moved to the body and renamed to `forceNewCertificates` (**plural!**) for the `api/execute` endpoint
* added `domainsToUpdate` parameter to `api/execute` endpoint. If the optional array is set then only certificates which contain one of the provided domains as a hostname are forcefully updated [#19](https://github.com/MarcStan/lets-encrypt-azure/issues/19)

See the [setup guide](https://github.com/MarcStan/lets-encrypt-azure/blob/master/docs/Setup.md#invoking-the-function-manually) for details.

# 2.0.1

* Bugfix for CDN updates: The internal update request now uses the correct encoding (utf-8) and no longer results in HTTP 415 (Unsupported Media Type)

# 2.0.0

The primary automation/deployment method is now Github Actions. The documentation has been updated and a [migration guide](./docs/Migration%20guide.md) exists to allow you to migrate from Azure Pipelines.

## infrastructure:

* declared `WEBSITE_RUN_FROM_PACKAGE=1` explicitely in ARM template (previously it was implicitly set by the publish task)
* Switch from Azure Pipelines to Github Actions for automation (old Azure Pipelines along with old setup guide can be found in the [last v1.x commit](https://github.com/MarcStan/lets-encrypt-azure/blob/89bec173830285c33e26f7d9bed476195b95fa5c/azure-pipelines.yml))

## api changes:

* Removed obsolete `updateResource` parameters from POST endpoint (function detects automatically if a resource needs to be updated)

# 1.1.1

* Upgrade to .Net Core 3.1

# 1.1.0

* Suggest custom role for least privilege access control [#11](https://github.com/MarcStan/lets-encrypt-azure/issues/11)
* Fixed logs being silenced [#9](https://github.com/MarcStan/lets-encrypt-azure/issues/9) & [#10](https://github.com/MarcStan/lets-encrypt-azure/issues/10)
* Retry CDN certificates when none is in progress or certificate does not match [#8](https://github.com/MarcStan/lets-encrypt-azure/issues/8)
* Switched to .Net Core 3.0 (and functions v3 runtime)
* Prevent unrelated app service certificates from being deleted (enforcing name + thumbprint match & fixed filter) [#7](https://github.com/MarcStan/lets-encrypt-azure/issues/7)
* Retry app service certificate rollout if certificate binding cannot be found (instead of silently skipping when cert is already in store) [#6](https://github.com/MarcStan/lets-encrypt-azure/issues/6)
* Property `path` of storage account challenge responder was not being used [#5](https://github.com/MarcStan/lets-encrypt-azure/issues/5)

# 1.0.1

* Bugfix regarding placement of sample configuration file [PR#2](https://github.com/MarcStan/lets-encrypt-azure/pull/2)

# 1.0.0 (initial release)

* Working function with support for Azure CDN & App Services


___

# Versioning considerations

All aspects of the function are taken into consideration when updating the version number. **If no explict mention is made of any of the below categories then no change was done for the specific category.**

## configuration file

Changes in the configuration file format are considered when they introduce breaking changes.

## infrastructure

Changes in infrastructure are technically automated via ARM templates (and the guidance is to use the provided ARM template). They are thus automatically applied when you deploy the function.

Nevertheless changes (adding/removing) in infrastructure will still be marked as a breaking change in the changelog.

## api

Internal api changes are currently not considered for versioning as the function entrypoints are considered to be the only public api surface.