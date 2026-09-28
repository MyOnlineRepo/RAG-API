# Tickets

Implementation tickets derived from [`SPEC.md`](../SPEC.md). The spec wins if a ticket and the spec disagree.

Tickets live here and **not** in `docs/` — `docs/` is the knowledge base that gets indexed.

## M1 — Foundation

Done when: the API starts, `/health/live` is healthy, and the author has reviewed `docs/` (SPEC §0).

| # | Ticket | Blocked by | Status |
|---|---|---|---|
| M1-01 | [Solution skeleton](M1-01-solution-skeleton.md) | — | done |
| M1-02 | [Configuration and Azure OpenAI wiring](M1-02-configuration-and-azure-openai.md) | M1-01 | done |
| M1-03 | [Index state and health endpoints](M1-03-health-endpoints.md) | M1-02 | done |
| M1-04 | [Knowledge base drafts](M1-04-knowledge-base.md) | M1-01 (guard test only) | done |

M1-04 can be worked on in parallel with M1-02 and M1-03; only its guard test needs the test project from M1-01.

## M2 — Retrieval

Done when: demo step 2 (SPEC §18) works against real Azure OpenAI; chunker and cache tests pass (SPEC §0).

| # | Ticket | Blocked by | Status |
|---|---|---|---|
| M2-01 | [Test host fakes and knowledge paths](M2-01-test-host-and-knowledge-paths.md) | — | done |
| M2-02 | [Markdown chunker](M2-02-markdown-chunker.md) | — | done |
| M2-03 | [Document source and embedding cache](M2-03-document-source-and-embedding-cache.md) | M2-01 | done |
| M2-04 | [Vector store and knowledge indexer](M2-04-vector-store-and-indexer.md) | M2-02, M2-03 | done |
| M2-05 | [Indexing on startup and `POST /index`](M2-05-startup-indexing-and-index-endpoint.md) | M2-04 | done |
| M2-06 | [`GET /search`, OpenAPI and Scalar](M2-06-search-endpoint-and-openapi.md) | M2-05 | done — author check open |

M2-01 and M2-02 are independent. The last criterion of M2-06 (demo step 2 against real Azure) needs the author.

## M3 — Generation · M4 — Evaluation & polish

Not cut yet. Cut after the previous milestone is done, so they reflect what was actually built.

## Conventions for every ticket

The full workflow (specification → implementation → validation → replanning) is in [`CLAUDE.md`](../CLAUDE.md).

- Build and all tests green: `dotnet build` and `dotnet test` at the repository root.
- No test calls Azure.
- No code, packages or folders for later milestones or Phase 2 ("not in this ticket" lists are binding).
- One ticket = one commit (or a small series), message references the ticket id, e.g. `M1-02: ...`.

## Validation log

Filled at the end of each milestone (see CLAUDE.md → Validation).

| Milestone | Date | Checked | Open / not checkable here |
|---|---|---|---|
| M1 | 2026-09-26 | All four tickets done. `dotnet build` 0 warnings, `dotnet test` 46/46 green. API starts with (fake) config; `/health/live` → 200; `/health/ready` → 503 `NotStarted` (expected: no index before M2). Missing config fails at startup with a clear message. Author reviewed and approved all nine `docs/` files. | No real Azure OpenAI call yet — first one happens in M2 indexing. Demo steps §18 not applicable before M2/M3. |
| M2 | 2026-09-28 | All six tickets implemented. `dotnet build` 0 warnings, `dotnet test` 106/106 green (10–20 consecutive full runs stable). "Done when" (SPEC §0): chunker and cache tests pass ✔; demo step 2 works end-to-end **with the bag-of-words fake** (index on startup → ready → search ranks `troubleshooting.md` first for the 503 query). Manually with an unreachable Azure endpoint: API starts, indexing fails cleanly, live 200 / ready 503 `Failed`, `POST /index` 500 and `/search` 503 as ProblemDetails; `/scalar` and `/openapi/v1.json` load in Development. Knowledge base: 67 chunks. | **Demo step 2 against real Azure OpenAI** (M2-06, last criterion) — needs the author's Azure resource and `az login`; top-5 scores to be recorded here. Until then M2 is not fully validated and replanning waits. |

## Replanning log

Filled between milestones (see CLAUDE.md → Replanning).

| After | Date | Learnings | Spec / roadmap changes |
|---|---|---|---|
| M1 | 2026-09-26 | (1) Deviations: empty strings instead of placeholders in `appsettings.json` (M1-02); no `live` tag needed (M1-03). (2) VectorData abstractions are stable (10.10.0), the InMemory connector is still preview (1.74.0-preview). (3) Relative `Knowledge` paths are ambiguous: `dotnet run --project` uses the project folder as content root, tests and Eval run from `bin/`. (4) SPEC §9 leaves open what readiness does during a *re*-index. (5) Once M2 adds the indexing hosted service, every test host would index `docs/` and call Azure unless the test factory fakes the AI clients by default. (6) The cloud container has no .NET SDK preinstalled. | Approved by the author on 2026-09-28 and written into SPEC: R1 paths → §12 + decision 12; R2 re-index readiness → §9 + decision 13 (plus `Knowledge:IndexOnStartup`); R3 test host fakes → §16 + decision 14; R4 preview connector → §6 + decision 15; R5 OpenAPI/Scalar in M2 → §0 + decision 16. Roadmap unchanged. |
| M2 | 2026-09-28 | Learnings in [replanning-after-M2.md](replanning-after-M2.md): preview connector incompatible with the newest abstraction (runtime `TypeLoadException`); no section needs splitting (67 chunks = sections); bag-of-words scores only valid as rankings; `WebApplicationFactory` startup race; Eval must use `AppContext.BaseDirectory` as content root. | Approved by the author on 2026-09-28 and written into SPEC: R6 VectorData pin → §6 + decision 17; R7 allowed `Application` packages → §5; R8 test-host rules → §16; R9 `RagService` reuses `KnowledgeSearch` → §10; R11 Eval content root → §15; R12 index example → §11. **R10 (provisional `MinScore`, §0) deferred until demo step 2 ran against real Azure.** Roadmap unchanged. |
