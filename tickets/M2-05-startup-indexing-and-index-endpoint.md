# M2-05 — Indexing on startup and `POST /index`

**Milestone:** M2 · **Blocked by:** M2-04 · **Spec:** §9, §11 (`POST /api/knowledge/index`, health)

## Goal

The API builds its index by itself on startup, `/health/ready` turns green, and the index can be rebuilt on demand.

## What to build

- `KnowledgeIndexingService : BackgroundService` (Api): if `Knowledge:IndexOnStartup` is `true`, calls
  `KnowledgeIndexer.RunAsync` once. Exceptions are caught and logged (state is already `Failed`) so the host keeps
  running and `/health/live` stays green.
- Endpoint group `/api/knowledge` (Api, `Endpoints/KnowledgeEndpoints.cs`):
  - `POST /api/knowledge/index` → `200` with `{ documents, chunks, embeddedNew, fromCache, durationMs }`;
    `409` ProblemDetails if a run is active; `500` ProblemDetails if indexing fails.
- `AddProblemDetails()` in `Program.cs`.
- Existing M1 health tests that expect `NotStarted` run with `Knowledge:IndexOnStartup=false`.

## Not in this ticket

- `GET /search`, OpenAPI/Scalar (M2-06).

## Acceptance criteria

- [x] Integration test: default test host → `/health/ready` becomes 200 (poll with timeout) and reports the chunk count.
- [x] Integration test: `IndexOnStartup=false` → `/health/ready` stays 503 `NotStarted`; `POST /index` → 200 with
      `chunks > 0`; afterwards `/health/ready` → 200.
- [x] Integration test: second `POST /index` → `embeddedNew = 0`, `fromCache = chunks`.
- [x] Integration test: `POST /index` while a run is active (blocking fake) → 409 ProblemDetails.
- [x] Integration test: failing embedding generator on startup → `/health/live` 200, `/health/ready` 503 with the error.
- [x] Manual, with a fake Azure endpoint (no Azure reachable): the API starts, logs the failed indexing attempt,
      `/health/live` → 200 and `/health/ready` → 503 `Failed` — no crash.

## Implementation notes

- `KnowledgeIndexingService` in `Api/Indexing`, endpoints in `Api/Endpoints/KnowledgeEndpoints.cs`
  (`MapKnowledgeEndpoints()`), response record `IndexResponse`. Handlers return `TypedResults` so OpenAPI (M2-06)
  can read the response types.
- `POST /index` runs with the application's stopping token, **not** the request's: a client that disconnects
  must not cancel a run and leave the index `Failed`.
- The 500 ProblemDetails `detail` carries the exception message — the same text `/health/ready` shows.
  Acceptable for a local demo without authentication (SPEC §11).
- Test hosts: `RealAiClientsApiFactory` now sets `IndexOnStartup=false` — otherwise the DI-registration tests
  would start an indexing run against Azure. The health tests and the indexer tests also run with
  `IndexOnStartup=false` because they drive `IndexState`/runs themselves. 10 consecutive full runs green.
- Manual check (2026-09-28): `AzureOpenAI__Endpoint=https://fake.openai.azure.com/`, no Azure credentials
  in the container. The API starts; the log shows `fail: KnowledgeIndexer — Indexing failed`
  (`CredentialUnavailableException` from `DefaultAzureCredential`) and the warning from the hosted service;
  `/health/live` → 200, `/health/ready` → 503 `Indexing failed: DefaultAzureCredential failed to retrieve a token…`;
  `POST /index` → 500 ProblemDetails with the same message. No crash. The long credential message is useful:
  it tells a developer to run `az login`.
- `dotnet run` uses `launchSettings.json` (port 5210) and ignores `ASPNETCORE_URLS` — relevant for the `.http`
  file in M2-06.
