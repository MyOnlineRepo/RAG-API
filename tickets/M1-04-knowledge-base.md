# M1-04 — Knowledge base drafts

**Milestone:** M1 · **Blocked by:** M1-01 (only for the guard test) · **Spec:** §4

## Goal

The nine CloudStore documents exist, are structured for the chunker, and contain the deliberate test cases —
guarded by a test so later edits cannot silently break the demo.

## What to build

**Documents** in `docs/` (English, 500–1,000 words each, one H1 title, content in H2 sections, realistic tone of
internal engineering docs): `architecture.md`, `authentication.md`, `security.md`, `api.md`, `deployment.md`,
`container-apps.md`, `storage.md`, `monitoring.md`, `troubleshooting.md` — content as in §4.2.

Must contain (§4.3, split facts):
- `storage.md`: uploads are stored in Azure Blob Storage, metadata in PostgreSQL. Detailed upload flow.
- `security.md`: Blob Storage and PostgreSQL are accessed via Managed Identity; no stored credentials; RBAC roles named.
- `troubleshooting.md`: section **"HTTP 503 after deployment"** with causes and checks — referring to "the health
  endpoints" and "revision status" **without** repeating their exact paths/commands.
- `monitoring.md`: `GET /health/live` and `GET /health/ready` with their meaning.
- `container-apps.md`: revisions, how to inspect revision status, probes wired to the health endpoints.
- `deployment.md`: push to main → restore → test → publish → Docker build → ACR → new revision; *what happens
  to the revision* is described in `container-apps.md`, not repeated.

Must **not** contain anywhere (§4.3, missing facts): a maximum upload size / file size limit, an SLA or
availability percentage, a backup retention period. `storage.md` talks about uploads extensively but never
mentions a limit (bait).

**Guard test** (`KnowledgeBaseTests` in `CloudKnowledge.Tests`, reads `docs/` from the repository root):
- Exactly the nine expected files exist.
- Each file has exactly one H1 and at least three H2 sections.
- Each file has 400–1,200 words (a little slack around the 500–1,000 target).
- Forbidden patterns do not occur (case-insensitive), e.g.: `\b\d+\s?(KB|MB|GB|TB)\b`, `max(imum)? (upload|file) size`,
  `size limit`, `\bSLA\b`, `\d{2}(\.\d+)?\s?%\s*(availability|uptime)`, `retention`, `retained for`.
- `troubleshooting.md` contains an H2 `HTTP 503 after deployment`; `monitoring.md` contains `/health/ready`.

## Not in this ticket

- `eval/questions.json` (M4) — but keep the split facts easy to phrase as questions.
- Any code that reads `docs/` besides the guard test.

## Acceptance criteria

- [ ] All nine files exist and the guard test passes.
- [ ] Reading the docs alone, a human can answer the §18 demo questions 3–5 and **cannot** answer question 6.
- [ ] **The author has read and approved every document** (this ticket is not done before that).
