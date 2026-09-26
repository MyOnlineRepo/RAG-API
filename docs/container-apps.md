# Running CloudStore on Azure Container Apps

## Environment and apps

CloudStore runs in the Container Apps environment `cae-cloudstore-prod` (and `cae-cloudstore-dev` for dev). The environment is connected to a virtual network so the apps can reach the private endpoints of PostgreSQL, Blob Storage and Key Vault.

The environment contains one container app, `ca-cloudstore-api`. It serves both the API and the static files of the Angular frontend. The app pulls its images from the Azure Container Registry `crcloudstore` using its managed identity.

## Revisions

Every deployment creates a new **revision** of the container app. A revision is an immutable snapshot of the app's container image and configuration. Changing the image tag, an environment variable or the scaling settings always creates a new revision.

CloudStore runs in **single revision mode**. When a new revision is created, Azure Container Apps:

1. Starts the replicas of the new revision.
2. Waits until the new replicas pass their startup and readiness probes.
3. Switches all traffic to the new revision.
4. Deactivates the previous revision.

If the new revision never becomes ready, traffic is not switched and the previous revision keeps serving requests for a while. The deployment is then reported as failed. Deactivated revisions are kept and can be reactivated for a rollback.

## Inspecting revision status

Each revision has a provisioning state and a running state. They show whether a deployment succeeded and whether the replicas are healthy.

To inspect the revision status:

- **Azure portal:** open the container app, then **Application → Revisions and replicas**. The list shows each revision with its running state, traffic weight and the number of replicas.
- **Azure CLI:** run `az containerapp revision list --name ca-cloudstore-api --resource-group rg-cloudstore-prod --output table`.

Important running states:

| State | Meaning |
|---|---|
| Running | The revision is active and its replicas are healthy. |
| Activating | The revision is starting and waiting for its probes to pass. |
| Degraded | Some replicas are failing. |
| Failed | The revision could not start, for example because the image could not be pulled or the container keeps crashing. |

To see why a replica fails, open **Revisions and replicas**, select the revision and look at the replica's container status and restart count, or read the system logs with `az containerapp logs show --name ca-cloudstore-api --resource-group rg-cloudstore-prod --type system`.

## Health probes

The container app has three probes configured, all pointing to the health endpoints of the API (the endpoints themselves are described in the monitoring document):

- **Startup probe** – uses the liveness endpoint. Gives the API time to start before other probes run.
- **Liveness probe** – uses the liveness endpoint. If it fails repeatedly, the container is restarted.
- **Readiness probe** – uses the readiness endpoint. A replica only receives traffic while this probe succeeds.

The readiness endpoint checks the database connection. A replica that cannot reach PostgreSQL is therefore not ready and receives no traffic.

## Scaling

The app scales horizontally based on concurrent HTTP requests:

- Minimum replicas: 2 in prod, 0 in dev.
- Maximum replicas: 10 in prod, 2 in dev.
- Scale rule: one additional replica per 50 concurrent requests.

Each replica has 1 vCPU and 2 Gi of memory. Because the API is stateless, replicas can be added and removed at any time.

## Ingress

The app uses external ingress with HTTPS only. The custom domain `app.cloudstore.example` is bound with a managed certificate. Session affinity is disabled.

## Configuration and environment variables

Configuration is passed to the container as environment variables, defined in the Bicep templates. The most important ones are:

| Variable | Purpose |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` or `Development` |
| `Storage__BlobEndpoint` | Endpoint of the storage account |
| `Database__Host` | Host name of the PostgreSQL server |
| `Database__Name` | Name of the database |
| `KeyVault__Uri` | URI of the Key Vault |
| `AzureAd__ClientId` | Client id of the CloudStore API app registration |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | Target for telemetry |

No environment variable contains a password or key. If a required variable is missing, the API fails at startup with a configuration validation error and the revision never becomes ready.

## Rolling back to a previous revision

To roll back, open **Revisions and replicas**, select the previous revision and activate it, then move all traffic to it. With the CLI: `az containerapp revision activate` followed by `az containerapp ingress traffic set`. The rollback takes effect within seconds because the previous image is already in the registry.
