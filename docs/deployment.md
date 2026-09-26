# Deployment Process

## Overview

CloudStore is deployed with GitHub Actions. Every change reaches production through the same automated pipeline; nobody deploys from a developer machine. The pipeline builds, tests and packages the API as a container image, pushes the image to Azure Container Registry and deploys it to Azure Container Apps.

The pipeline definition lives in `.github/workflows/cloudstore.yml` in the CloudStore repository.

## Branching model

- All work happens on short-lived feature branches.
- Changes are merged into `main` through pull requests. A pull request needs one approval and a green build.
- Direct pushes to `main` are blocked by branch protection.
- Releases to prod are created by pushing a tag of the form `v1.2.3` on a commit of `main`.

## Pipeline stages

When a new version is pushed to the `main` branch — in practice, when a pull request is merged — the CI pipeline starts automatically. It runs the following stages in order. If a stage fails, the pipeline stops and nothing is deployed.

1. **Restore** – `dotnet restore` downloads all NuGet packages. Packages are cached between runs.
2. **Build** – `dotnet build` compiles the solution in Release configuration. Warnings are treated as errors.
3. **Test** – `dotnet test` runs the unit tests and the integration tests. Integration tests use a PostgreSQL container started inside the pipeline.
4. **Publish** – `dotnet publish` creates the deployable output of the API.
5. **Frontend build** – the Angular application is built with `npm ci` and `ng build` and copied into the API image as static files.
6. **Docker build** – the pipeline builds the container image with the Dockerfile in the repository root. The image is tagged with the Git commit SHA.
7. **Push to registry** – the image is pushed to the Azure Container Registry `crcloudstore`.
8. **Database migrations** – pending schema migrations are applied to the database of the target environment.
9. **Deploy** – the pipeline updates the container app with the new image. Azure Container Apps then creates a new revision.

What happens with the new revision — how traffic is switched and how the old revision is deactivated — is described in the Container Apps document.

## Deployment to dev and prod

- A push to `main` runs all stages and deploys to **dev**.
- A version tag runs the same stages and deploys to **prod**. The prod deployment requires an approval in the GitHub `production` environment by a member of the operations team.

The image that is deployed to prod is built from the tagged commit. The pipeline does not promote images from dev to prod; both environments always receive an image built from exactly the commit that is deployed.

## Authentication of the pipeline

The pipeline authenticates to Azure with OpenID Connect (workload identity federation). A federated credential on the pipeline's Entra ID app registration trusts tokens issued by GitHub for the CloudStore repository and the `main` branch or version tags. No Azure client secret is stored in GitHub.

The pipeline identity has the role `AcrPush` on the container registry and permission to update the container apps and run migrations. It has no access to the data in Blob Storage.

## Database migrations

Schema migrations are written with Entity Framework Core and applied by the pipeline before the new image is deployed. Migrations must be backwards compatible with the currently running version of the API, because the old revision keeps serving traffic until the new revision is ready. Columns are therefore removed in two steps: first the code stops using the column, and only a later release drops it.

## Rolling back

If a deployment causes problems, the fastest way back is to reactivate the previous revision in Azure Container Apps (see the Container Apps document). A rollback through the pipeline — reverting the commit and pushing to `main` — is used when the fix has to be permanent.

## Infrastructure changes

Changes to Azure resources are made in the Bicep templates in the `infra/` folder. A separate workflow, `infra.yml`, validates the templates on every pull request with `what-if` and applies them after merge. Application deployments never change infrastructure.
