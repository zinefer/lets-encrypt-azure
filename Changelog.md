# Changelog

Versioning is done by tagging commits on master and is SemVer compliant.

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