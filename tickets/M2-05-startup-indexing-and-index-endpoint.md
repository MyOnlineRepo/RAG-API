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

- [ ] Integration test: default test host → `/health/ready` becomes 200 (poll with timeout) and reports the chunk count.
- [ ] Integration test: `IndexOnStartup=false` → `/health/ready` stays 503 `NotStarted`; `POST /index` → 200 with
      `chunks > 0`; afterwards `/health/ready` → 200.
- [ ] Integration test: second `POST /index` → `embeddedNew = 0`, `fromCache = chunks`.
- [ ] Integration test: `POST /index` while a run is active (blocking fake) → 409 ProblemDetails.
- [ ] Integration test: failing embedding generator on startup → `/health/live` 200, `/health/ready` 503 with the error.
- [ ] Manual, with a fake Azure endpoint (no Azure reachable): the API starts, logs the failed indexing attempt,
      `/health/live` → 200 and `/health/ready` → 503 `Failed` — no crash.
