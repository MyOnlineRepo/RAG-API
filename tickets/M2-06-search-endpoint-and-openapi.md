# M2-06 — `GET /search`, OpenAPI and Scalar

**Milestone:** M2 · **Blocked by:** M2-05 · **Spec:** §11 (`GET /api/knowledge/search`), §0 (decision 16), §18 step 2

## Goal

Retrieval becomes visible: a question goes in, the most similar documentation sections come out with scores —
without any chat model. This is demo step 2.

## What to build

- `KnowledgeSearch` (Application): `Task<SearchResult> SearchAsync(string query, int top, CancellationToken)`
  - embeds the query with `IEmbeddingGenerator` (queries are **not** cached),
  - vector search in the `knowledge` collection, top `top`,
  - maps each hit to `SearchHit(Document, Section, Score, AboveThreshold, Content)` with
    `AboveThreshold = Score >= Rag:MinScore`. Results are returned regardless of `MinScore` (SPEC §11).
  - `Score` is cosine similarity (higher = more similar); verify what the InMemory connector returns and convert if needed.
- `GET /api/knowledge/search?q=&top=` in `KnowledgeEndpoints`:
  - `q` required, 3–1000 characters; `top` optional, default `Rag:TopK`, 1–20 → otherwise 400 ProblemDetails.
  - `503` ProblemDetails while `IndexState` is not `Ready`.
  - Response shape exactly as SPEC §11 (`query`, `minScore`, `results[]`).
- OpenAPI via `Microsoft.AspNetCore.OpenApi` (`/openapi/v1.json`) and Scalar UI (`/scalar`) in Development only.
  Endpoints have names, summaries and response types so the UI is self-explanatory.
- `src/CloudKnowledge.Api/CloudKnowledge.Api.http` with the demo requests: health, index, and the search from
  demo step 2 plus two more searches (503 question, upload question).

## Not in this ticket

- `POST /ask`, `MinScore` calibration (M3/M4).

## Acceptance criteria

- [x] Integration test (bag-of-words fake): `q=HTTP 503 after deployment` → first hit is
      `troubleshooting.md` / `HTTP 503 after deployment`; scores are descending.
- [x] Integration test: `top=3` → 3 results; `aboveThreshold` follows `MinScore` (set e.g. to 0.5 in the test).
- [x] Integration test: missing/too short `q`, `top=0`, `top=21` → 400 ProblemDetails.
- [x] Integration test: index not ready (`IndexOnStartup=false`) → 503 ProblemDetails.
- [x] `/openapi/v1.json` lists `/search`, `/index` with their responses; `/scalar` loads in Development and is absent in Production.
- [ ] **Author, against real Azure OpenAI (demo step 2):** `GET /search?q=How does the deployment process work?`
      returns `deployment.md` and `container-apps.md` at the top. Result (top 5 with scores) recorded in the M2
      validation log — these real scores are the first input for `MinScore` calibration.

## Implementation notes

- **Package version conflict found and fixed.** `Microsoft.SemanticKernel.Connectors.InMemory` 1.74.0-preview (the
  newest version, checked 2026-09-28) is built against `Microsoft.Extensions.VectorData.Abstractions` **10.1.0** and
  uses `VectorSearchFilter`, which 10.10.0 removed. With 10.10.0 every vector search failed at runtime with
  `TypeLoadException` (M2-04 did not notice: upsert/get do not touch that type). `Application` now pins
  Abstractions to **10.1.0** — the stable version the connector declares as its dependency.
  **SPEC §6 still says 10.10.0** — it is part of the constitution, so the correction is proposed for the
  M2 replanning instead of being edited here.
- `KnowledgeSearch` lives in `Application/Search` with `SearchHit` and `SearchResult`; the endpoint returns
  `SearchResult` directly (camelCase matches SPEC §11).
- Score: the InMemory connector returns cosine **similarity** for `DistanceFunction.CosineSimilarity`
  (verified by a test: a chunk's own text scores ≈ 1.0, a distance would be ≈ 0). No conversion needed.
- `q` is trimmed before the length check. Validation errors are `ValidationProblem` (400 with an `errors` map).
- The bag-of-words fake gives low scores for whole sections (best hit ≈ 0.18), so the `aboveThreshold` test uses
  `MinScore = 0.15` instead of the ticket's example 0.5.
- OpenAPI: endpoints have names (`SearchKnowledge`, `RebuildIndex`), summaries, descriptions and documented
  400/409/500/503 responses. `/openapi/v1.json` and `/scalar` exist only in Development (tests for Development
  and Production).
- Manual check (2026-09-28, fake endpoint, Development): `/scalar` → 302 → `/scalar/` 200 "Scalar API Reference";
  `/openapi/v1.json` lists search (200/400/503) and index (200/409/500); `q=ab` → 400 ProblemDetails;
  search while `Failed` → 503 ProblemDetails "Index status: Failed".
- `.http` file uses `http://localhost:5210` from `launchSettings.json`.
