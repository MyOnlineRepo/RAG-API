# M1-03 — Index state and health endpoints

**Milestone:** M1 · **Blocked by:** M1-02 · **Spec:** §9 (readiness), §11 (health)

## Goal

Liveness and readiness exist before there is anything to index, so M2 only has to flip the state.
In M1 the API is live but **not ready** — which is correct, because no index exists yet.

## What to build

- `IndexState` (in `Application`), registered as singleton:
  - `Status`: `NotStarted | Indexing | Ready | Failed`
  - `ChunkCount`, `LastIndexedAt` (nullable), `Error` (nullable string)
  - Thread-safe transitions: `MarkIndexing()`, `MarkReady(int chunkCount)`, `MarkFailed(string error)`.
- Health checks (ASP.NET Core `AddHealthChecks`):
  - `GET /health/live` — no checks, always `Healthy` while the process runs.
  - `GET /health/ready` — `IndexReadinessHealthCheck`: `Healthy` only when `Status == Ready`,
    otherwise `Unhealthy` with the status (and error, if failed) in the description.
  - Tag-based filtering (`live` / `ready`) so each endpoint runs only its checks.
- Response body: JSON with overall status and the check description (small custom `ResponseWriter`),
  so the demo can show *why* the API is not ready.

## Implementation notes

- `IndexState` exposes an immutable `IndexSnapshot` (`Current`); transitions swap it atomically, so the
  readiness check never sees a half-updated state.
- `MarkIndexing()` and `MarkFailed()` keep the last successful chunk count and timestamp; `LastIndexedAt`
  comes from an injected `TimeProvider` (testable).
- `IndexState` and `TimeProvider.System` are registered in `AddCloudKnowledge()`; the health check and the
  JSON response writer live in `Api/Health`.

## Not in this ticket

- Any code that sets the state to `Indexing` / `Ready` (M2 indexing service).
- The 503 behaviour of `/ask` and `/search` while not ready (comes with those endpoints).

## Acceptance criteria

- [x] Integration test: `/health/live` returns 200.
- [x] Integration test: `/health/ready` returns 503 with status `NotStarted` in the body on a fresh app.
- [x] Integration test: after `IndexState.MarkReady(10)` (resolved from the test host), `/health/ready` returns 200.
- [x] Integration test: after `MarkFailed("boom")`, `/health/ready` returns 503 and the body contains `boom`.
- [x] Unit tests for `IndexState` transitions.
