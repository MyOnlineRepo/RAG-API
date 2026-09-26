# CloudStore Architecture Overview

## Purpose of CloudStore

CloudStore is a multi-tenant SaaS application for managing business documents. Customers upload contracts, invoices and other files, organise them into folders, tag them and share them with colleagues inside their organisation. The product consists of a browser-based frontend, a backend API and a small set of managed Azure services.

This document gives a high-level overview of the system. Details about individual areas are described in the dedicated documents: authentication, security, API, deployment, Container Apps, storage, monitoring and troubleshooting.

## Components

CloudStore consists of the following components:

- **Angular SPA** – the single-page application that users interact with in the browser. It is built as static files and served from the same Container Apps environment as the API.
- **CloudStore API** – an ASP.NET Core Web API written in C#. It contains all business logic, validates requests and talks to the data stores.
- **PostgreSQL** – an Azure Database for PostgreSQL Flexible Server (`psql-cloudstore-prod`). It stores all transactional data: tenants, users, folders, tags, sharing information and document metadata.
- **Azure Blob Storage** – the storage account `stcloudstoreprod`. It stores the binary content of uploaded documents.
- **Azure Container Apps** – the hosting platform for the API and the frontend.
- **Azure Container Registry** – the registry `crcloudstore` that holds the container images built by the CI pipeline.
- **Azure Key Vault** – `kv-cloudstore-prod`, used for the few configuration values that are sensitive but are not credentials, such as signing keys for share links.
- **Application Insights** – collects telemetry, logs, traces and metrics from the API and the frontend.
- **Microsoft Entra ID** – the identity provider for all users.

## Request flow

A typical request travels through the system like this:

1. The user opens CloudStore in the browser. The Angular application is loaded and redirects the user to Microsoft Entra ID to sign in.
2. After sign-in, the Angular application holds an access token for the CloudStore API.
3. The Angular frontend communicates with the API exclusively over HTTPS. Every API call carries the access token in the `Authorization` header.
4. The API validates the token, resolves the tenant and the user, and executes the request.
5. For metadata operations (listing folders, searching by tag, renaming a document) the API only talks to PostgreSQL.
6. For content operations (uploading or downloading a file) the API additionally talks to Azure Blob Storage.
7. The response is returned to the frontend as JSON, or as a file stream for downloads.

## Stateless API

The API is stateless. It does not keep user sessions, uploaded files or caches in local memory or on the local file system. All state lives in PostgreSQL or in Blob Storage.

This has several consequences:

- Any replica of the API can handle any request. There is no session affinity.
- Replicas can be added or removed at any time by the scaling rules of Azure Container Apps.
- A new revision can replace the old one without migrating in-memory state.
- Temporary files created during an upload are written to a stream and never persisted on the container's disk.

## Where data lives

The split between the two data stores is a core design decision of CloudStore:

| Data | Store |
|---|---|
| Tenants, users, roles | PostgreSQL |
| Folders, tags, sharing | PostgreSQL |
| Document metadata (name, owner, content type, checksum, blob path) | PostgreSQL |
| Document content (the file itself) | Azure Blob Storage |
| Audit events | PostgreSQL |
| Telemetry and logs | Application Insights |

PostgreSQL is the source of truth for which documents exist. A blob without a matching metadata row is considered orphaned and is removed by a nightly cleanup job.

## Environments

CloudStore runs in two environments:

- **dev** – used by the development team. Every merge to the `main` branch is deployed here automatically.
- **prod** – used by customers. Deployments to prod are triggered by creating a release tag.

Both environments use the same architecture and the same infrastructure templates. They differ only in resource names, scaling settings and the Entra ID app registrations.

## Design principles

- **Managed services first.** The team does not operate virtual machines or Kubernetes clusters. Everything runs on managed Azure services.
- **No credentials in configuration.** The application does not contain passwords or connection-string secrets. How Azure services are accessed is described in the security document.
- **Infrastructure as code.** All Azure resources are defined in Bicep templates in the `infra/` folder of the CloudStore repository.
- **Observable by default.** Every request is traced end to end in Application Insights, from the Angular frontend to the database.
