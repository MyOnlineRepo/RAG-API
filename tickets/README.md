# Tickets

Implementation tickets derived from [`SPEC.md`](../SPEC.md). The spec wins if a ticket and the spec disagree.

Tickets live here and **not** in `docs/` — `docs/` is the knowledge base that gets indexed.

## M1 — Foundation

Done when: the API starts, `/health/live` is healthy, and the author has reviewed `docs/` (SPEC §0).

| # | Ticket | Blocked by | Status |
|---|---|---|---|
| M1-01 | [Solution skeleton](M1-01-solution-skeleton.md) | — | open |
| M1-02 | [Configuration and Azure OpenAI wiring](M1-02-configuration-and-azure-openai.md) | M1-01 | open |
| M1-03 | [Index state and health endpoints](M1-03-health-endpoints.md) | M1-02 | open |
| M1-04 | [Knowledge base drafts](M1-04-knowledge-base.md) | M1-01 (guard test only) | open |

M1-04 can be worked on in parallel with M1-02 and M1-03; only its guard test needs the test project from M1-01.

## M2 — Retrieval · M3 — Generation · M4 — Evaluation & polish

Not cut yet. Cut after M1 is done so they reflect what was actually built.

## Conventions for every ticket

- Build and all tests green: `dotnet build` and `dotnet test` at the repository root.
- No test calls Azure.
- No code, packages or folders for later milestones or Phase 2 ("not in this ticket" lists are binding).
- One ticket = one commit (or a small series), message references the ticket id, e.g. `M1-02: ...`.
