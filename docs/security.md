# Security and Service Access

## Principles

CloudStore follows a small set of security principles that apply to every component:

- **No stored credentials.** The application does not store passwords, account keys or connection-string secrets for Azure services — not in configuration files, not in environment variables and not in Key Vault.
- **Identity-based access.** Every access from the API to an Azure service is authenticated with Microsoft Entra ID.
- **Least privilege.** Each identity receives only the roles it needs, scoped to the individual resource and not to the whole subscription.
- **Encryption everywhere.** All traffic uses TLS. Data at rest is encrypted by the Azure platform.

End-user sign-in is described in the authentication document. This document covers how the CloudStore API accesses Azure services.

## Managed Identity

The CloudStore API uses a Managed Identity to authenticate against Azure services. Each Container App has a system-assigned managed identity that is created and deleted together with the app. Azure manages the credentials of this identity and rotates them automatically; the team never sees or handles them.

In code, the API uses `DefaultAzureCredential` from the Azure.Identity library. When running in Azure Container Apps, it resolves to the managed identity of the container app. When running on a developer machine, it resolves to the developer's Azure CLI or Visual Studio sign-in. The same code works in both places without configuration changes.

## Access to Blob Storage

Blob Storage access uses Managed Identity. The API does not use storage account keys or shared access signatures for its own operations. Shared key access is disabled on the storage account `stcloudstoreprod`, so key-based access is not possible even if a key were leaked.

The managed identity of the API has the role **Storage Blob Data Contributor** on the storage account. This allows it to read, write and delete blobs, but not to change the storage account configuration.

Downloads are streamed through the API. Users never receive direct links to the storage account, so all access checks happen in the API.

## Access to PostgreSQL

The API connects to Azure Database for PostgreSQL with Microsoft Entra authentication. The managed identity of the API is registered as a database role in the PostgreSQL server. Instead of a password, the API requests an access token for the resource `https://ossrdbms-aad.database.windows.net` and passes it as the password when opening a connection. Tokens are refreshed automatically before they expire.

The database role of the API has read and write permissions on the application schema, but no permission to create or drop databases. Schema migrations run in the CI pipeline with a separate identity.

## Access to Key Vault

The API reads a small number of non-credential secrets from Key Vault, such as the signing key for share links. The managed identity has the role **Key Vault Secrets User** on `kv-cloudstore-prod`, which allows reading secrets but not creating or changing them.

## Role assignments overview

| Identity | Resource | Role |
|---|---|---|
| Container App `ca-cloudstore-api` (system-assigned) | Storage account `stcloudstoreprod` | Storage Blob Data Contributor |
| Container App `ca-cloudstore-api` (system-assigned) | PostgreSQL `psql-cloudstore-prod` | Database role `cloudstore_app` |
| Container App `ca-cloudstore-api` (system-assigned) | Key Vault `kv-cloudstore-prod` | Key Vault Secrets User |
| Container App `ca-cloudstore-api` (system-assigned) | Container Registry `crcloudstore` | AcrPull |
| CI pipeline identity | Container Registry `crcloudstore` | AcrPush |

All role assignments are defined in the Bicep templates. Manual role assignments in the portal are not allowed and are removed by the next deployment.

## Network security

The storage account, the PostgreSQL server and the Key Vault only accept traffic from the virtual network of the Container Apps environment. Public network access is disabled for all three resources. The API itself is exposed through the Container Apps ingress with HTTPS only; HTTP requests are redirected.

## Common mistakes

- **Missing role assignment.** A new resource or a recreated container app has a new identity. If the role assignment is missing, requests fail with HTTP 403 from the Azure service, although the code is correct.
- **Role assignment delay.** New role assignments can take several minutes to become effective.
- **Wrong credential locally.** If a developer is signed in to the wrong tenant in the Azure CLI, `DefaultAzureCredential` picks up that account and access is denied.
