# Monitoring and Observability

## Overview

CloudStore sends all telemetry to Application Insights (`appi-cloudstore-prod`), which is backed by a Log Analytics workspace. The API, the Angular frontend and the Container Apps platform all report to the same workspace, so a request can be followed from the browser to the database in a single end-to-end trace.

The API uses OpenTelemetry through the Azure Monitor OpenTelemetry distro for ASP.NET Core. The frontend uses the Application Insights JavaScript SDK.

## Health endpoints

The API exposes two health endpoints. They do not require authentication and are used by the Container Apps probes and by external availability tests.

The liveness endpoint is:

```
GET /health/live
```

It returns HTTP 200 as long as the process is running and able to handle HTTP requests. It performs no checks against other services. If it fails, the process is considered broken and the container is restarted.

The readiness endpoint is:

```
GET /health/ready
```

It returns HTTP 200 only if the API can handle real requests. It checks:

- the connection to PostgreSQL (a simple query is executed),
- that all pending database migrations have been applied,
- that the configuration was loaded successfully.

If any check fails, the endpoint returns HTTP 503 and a JSON body listing each check with its status. A replica whose readiness check fails receives no traffic, but it is not restarted.

The readiness endpoint intentionally does not check Blob Storage. A temporary storage problem should only affect uploads and downloads, not take the whole API out of rotation.

## Structured logging

The API writes structured logs with `ILogger` and message templates. Every log entry contains the trace id, the tenant id and the user id, which makes it possible to filter all log entries of a single request or a single customer.

Log levels in prod:

- `Information` for the application's own categories,
- `Warning` for ASP.NET Core and Entity Framework Core,
- `Error` for unhandled exceptions, which are also recorded as exceptions in Application Insights.

File contents and access tokens are never logged.

## Metrics

Besides the standard request and dependency metrics, the API records custom metrics:

| Metric | Description |
|---|---|
| `cloudstore.uploads.count` | Number of completed uploads, by content type |
| `cloudstore.uploads.duration` | Duration of the upload flow up to the malware scan |
| `cloudstore.scan.rejected` | Number of files rejected by the malware scan |
| `cloudstore.db.pool.usage` | Usage of the PostgreSQL connection pool |

## Dashboards and queries

The shared Azure dashboard "CloudStore – Operations" shows request rate, failure rate, response time percentiles, replica count and the custom metrics above.

Useful Kusto queries in Log Analytics:

- Failed requests of the last hour: `requests | where timestamp > ago(1h) and success == false | summarize count() by resultCode, name`
- Logs of a single request: `traces | where operation_Id == "<traceId>"`
- Container restarts: `ContainerAppSystemLogs_CL | where Reason_s == "BackOff"`

The trace id is returned in every error response of the API (see the API document), so a support ticket can be linked directly to its trace.

## Alerts

Alerts are defined in Bicep and sent to the operations team's Microsoft Teams channel:

- Failure rate above 5 % over 10 minutes.
- Server response time (95th percentile) above 2 seconds over 15 minutes.
- Readiness endpoint failing on all replicas for more than 2 minutes.
- More than 3 container restarts within 10 minutes.
- Malware scan rejected a file.

## Availability tests

Application Insights runs a standard availability test against the readiness endpoint of prod from five Azure regions every five minutes. A failed test from at least three regions triggers an alert.
