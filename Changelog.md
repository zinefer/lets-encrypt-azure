# Changelog

Versioning is done by tagging commits on master and is SemVer compliant.


# vNext

* Prevent unrelated app service certificates from being deleted (enforcing name + thumbprint match & fixed filter) [#7](https://github.com/MarcStan/lets-encrypt-azure/issues/7)
* Retry app service certificate rollout if certificate binding cannot be found (instead of silently skipping when cert is already in store) [#6](https://github.com/MarcStan/lets-encrypt-azure/issues/6)
* Property `path` of storage account challenge responder was not being used [#5](https://github.com/MarcStan/lets-encrypt-azure/issues/5)

# 1.0.1

* Bugfix regarding placement of sample configuration file [PR#2](https://github.com/MarcStan/lets-encrypt-azure/pull/2)

# 1.0.0 (initial release)

* Working function with support for Azure CDN & App Services