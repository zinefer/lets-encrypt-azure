# Migrate from Azure Pipelines to Github Actions

This guide aims to help you switch from the Azure Pipelines deployment model to use the new Github Action based model that I introduced in v2.0.0.

At this time (v2.0.0) the new deployment is identical (content-wise) to the existing azure pipeline (see also the [changelog](../Changelog.md)). In the future I might introduce further changes that I will not be backported to Azure Pipelines as such I recommend you migrate.

The migration is painless and removes the dependency of having an Azure DevOps account (since Github Actions now provides sufficient CI/CD capabilities).

## Updating your repository

If you previously followed the setup guide then you should only have modified the `azure-pipelines.yml` which has now been removed.

As such syncing the latest changes into your fork should be easy. There is also a way to do it [directly in the Github UI](https://stackoverflow.com/questions/20984802/how-can-i-keep-my-fork-in-sync-without-adding-a-separate-remote/21131381#21131381).

## Setup Github Actions

Much like Azure DevOps Github also needs permission to deploy to Azure. You can follow the [Setting up the deployment](./Setup.md#Setting-up-the-deployment) section in the setup guide.

At this point you are already done with the migration and everything should (continue to) work as expected.

## Cleanup Azure DevOps

In Azure DevOps a service connection was created that has permission to deploy to Azure if you created it specifically for this function you may want to delete it now. 

The Azure DevOps service connection is backed by a Service principal in Azure. You can delete it by going to `Azure` -> `Active Directory` -> `App registrations` -> `All applications` (obviously only delete it if you are not using the service principal/service connection in other deployments).

If you have only created it for this function/have already removed the service principal you can also remove the service connection by going to your `Azure DevOps project` -> `project settings` -> `service connections` and deleting it.
