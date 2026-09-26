# Troubleshooting Guide

This guide lists common problems in CloudStore and how to investigate them. For every problem, first find the trace id of a failing request (it is part of every error response) and look up the request in Application Insights.

## HTTP 503 after deployment

Symptom: after a deployment, the application responds with HTTP 503 Service Unavailable, either for all requests or for a part of them.

HTTP 503 after a deployment usually means that the new revision did not become ready, so Container Apps has no healthy replica to route traffic to.

Possible causes:

1. The container fails to start, for example because of an exception during startup.
2. The health endpoints are not reachable or report an unhealthy state.
3. Required environment variables are missing or have wrong values.
4. The API cannot connect to the PostgreSQL database.
5. The container image cannot be started or pulled from the registry.

Recommended checks, in this order:

1. **Inspect the revision status** of the container app. A revision in the state `Activating`, `Degraded` or `Failed` explains the 503.
2. **Check the application logs** of the new revision in Log Analytics or with the Container Apps log stream. Look for exceptions during startup.
3. **Verify the readiness endpoint.** Call it on the new revision; its JSON response shows which check fails.
4. **Verify the environment configuration.** Compare the environment variables of the new revision with the previous one. A configuration validation error at startup points to a missing value.
5. **Check PostgreSQL connectivity.** Make sure the database server is running, that the managed identity still has its database role and that no firewall or network change blocks the connection.

If the problem cannot be fixed quickly, roll back to the previous revision first and investigate afterwards.

## HTTP 401 from the API

Symptom: all API calls from the frontend fail with HTTP 401 Unauthorized.

Possible causes:

- The access token has the wrong audience because the frontend requests a scope of a different app registration, for example the dev registration in prod.
- The user's organisation is not registered as a CloudStore tenant, so the issuer is rejected.
- The system clock of a developer machine is wrong, which makes tokens appear expired.

Checks: decode the access token (without sending it anywhere else) and compare `aud`, `iss` and `scp` with the values expected by the API. Check the API logs for the reason the JWT handler rejected the token.

## HTTP 403 when accessing a document

Symptom: a user can sign in but gets HTTP 403 for certain folders or actions.

This is usually expected behaviour. Check which app role the user has in their organisation and whether the folder has been shared with the user or one of their groups. Tenant administrators can see the shares of a folder in the sharing settings.

## Uploads fail

Symptom: uploading a file fails, while listing and downloading existing documents still works.

Possible causes:

- **Missing role assignment on the storage account.** After the container app was recreated, its managed identity is new and has not received its storage role yet. The API logs show HTTP 403 responses from Blob Storage.
- **Content type not allowed.** The API returns HTTP 415. The list of accepted content types is in the storage document.
- **Malware scan rejection.** The upload succeeds technically, but the document ends in the status `Rejected`. The file can be found in the quarantine container.
- **Wrong storage endpoint.** The environment variable for the blob endpoint points to the storage account of another environment.

Checks: search the logs for the upload's trace id and look at the dependency calls to Blob Storage and their result codes.

## Slow responses

Symptom: the application works, but responses are noticeably slower than usual.

Checks:

1. Look at the response time dashboard to see whether all endpoints or only some are affected.
2. Check the dependency durations in Application Insights. Slow PostgreSQL queries are the most common cause.
3. Check the connection pool metric. A saturated pool leads to requests waiting for a database connection.
4. Check the replica count. If the app is at its maximum number of replicas, the scale rule cannot add capacity.

## Container keeps restarting

Symptom: the restart count of the replicas increases continuously.

A repeatedly failing liveness probe or a crash during startup causes restarts. Read the system logs of the container app to see the exit code and reason, and the application logs for the last exception before the restart.
