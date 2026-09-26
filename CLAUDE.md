# CLAUDE.md

CloudKnowledge.Api — a small RAG demo (ASP.NET Core + Azure OpenAI) answering questions about the fictional
"CloudStore" system from a local Markdown knowledge base. This repo is built with **spec-driven development**.

## Workflow: spec-driven development

```
Constitution ──► Feature phase (M1) ──► Replanning ──► Feature phase (M2) ──► Replanning ──► ...
                 Specification → Implementation → Validation
```

### 1. Constitution — stable, rarely changes

The rules every phase must follow. Lives in `SPEC.md`:

| What | Where |
|---|---|
| Mission, goals, non-goals, audience | SPEC §1–§3 |
| Architecture and tech stack | SPEC §5, §6 |
| Phases and milestones (roadmap) | SPEC §0 |
| Decisions and their reasons | SPEC §20 (decision log) |
| Conventions for working in this repo | this file |

**Changing the constitution needs the author's explicit approval.** Propose the change with a reason; never
edit these sections as a side effect of implementing a ticket.

### 2. Feature phase = one milestone (M1, M2, …, Phase 2)

Each phase runs the three steps in order. Do not start a step before the previous one is finished.

**a) Specification**
- The milestone's detailed behaviour is in its SPEC sections (see the milestone table in SPEC §0).
- It is cut into tickets in `tickets/` (`M<n>-<nn>-<slug>.md`) with goal, what to build, *Not in this ticket*,
  and checkable acceptance criteria. `tickets/README.md` is the index with status and dependencies.
- Tickets are cut **only at the start of their phase**, never ahead — later phases are cut after replanning.
- The author reviews the tickets before implementation starts.

**b) Implementation**
- One ticket at a time, in dependency order. Set its status in `tickets/README.md`.
- Build exactly what the ticket says. *Not in this ticket* lists are binding; no code for later milestones or Phase 2.
- **Spec gap or conflict found?** Stop and ask. Do not guess and do not silently deviate.
  Small, clearly better deviations may be made but must be written into the ticket (what and why) and reported.
- Commit per ticket, message starts with the ticket id (`M1-03: ...`). Push after each ticket.

**c) Validation**
- Per ticket: `dotnet build` with 0 warnings, `dotnet test` green, and every acceptance criterion **actually
  verified** (run it, don't assume). Tick a checkbox only after verifying it. Then mark the ticket `done`.
- Per milestone: check the milestone's "done when" from SPEC §0, and the relevant demo steps from SPEC §18
  where possible. Record the result in the **Validation log** in `tickets/README.md` (date, what was checked,
  what could not be checked and why — e.g. "needs a real Azure OpenAI resource").
- Anything that needs the author (reviewing `docs/`, running against real Azure) is listed as open, not ticked.

### 3. Replanning — between phases

After a milestone is validated and before the next one is cut:
1. Summarise what was learned: deviations recorded in tickets, surprises, things that turned out easier/harder.
2. Propose SPEC updates (sections for later milestones, new decision-log entries) — author approves.
3. Update the roadmap in SPEC §0 if scope or order changes.
4. Add an entry to the **Replanning log** in `tickets/README.md`.
5. Only then cut the next milestone's tickets.

## Repository map

```
SPEC.md                           constitution + full specification (source of truth)
tickets/                          tickets per milestone, index, validation log, replanning log
docs/                             CloudStore knowledge base — the data that gets indexed, NOT project docs
src/CloudKnowledge.Api            Minimal API host
src/CloudKnowledge.Application    core logic and abstractions, no Azure dependencies
src/CloudKnowledge.Infrastructure Azure OpenAI, vector store, file access, AddCloudKnowledge() DI extension
tests/CloudKnowledge.Tests        xUnit unit + integration tests
```

## Commands

```
dotnet build                      # must report 0 warnings (warnings are errors)
dotnet test                       # never calls Azure
dotnet run --project src/CloudKnowledge.Api
```

Local Azure values go into user secrets (`dotnet user-secrets set "AzureOpenAI:Endpoint" ... --project src/CloudKnowledge.Api`).

## Code conventions

- .NET 10, nullable enabled, file-scoped namespaces, warnings as errors.
- Dependency direction: `Api → Infrastructure → Application`. `Application` never references Azure packages.
- Configuration via options classes in `Application/Configuration` with data annotations and `ValidateOnStart`.
- Azure authentication only via `DefaultAzureCredential`. No API keys, no secrets in the repo.
- Tests use `CloudKnowledgeApiFactory` and the fakes in `tests/CloudKnowledge.Tests/Fakes`; no test calls Azure.
- Structured logging with message templates, no string interpolation in log calls.
- Match the style of surrounding code; keep it small — this is a demo that must be explainable line by line.
