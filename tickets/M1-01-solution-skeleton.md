# M1-01 — Solution skeleton

**Milestone:** M1 · **Blocked by:** — · **Spec:** §5, §6

## Goal

An empty but correctly wired solution that builds and runs one test. Every later ticket adds code into this
structure instead of deciding structure again.

## What to build

- `CloudKnowledge.sln` at the repository root with:
  - `src/CloudKnowledge.Api` — ASP.NET Core, Minimal API (`web` template, trimmed: no weather sample)
  - `src/CloudKnowledge.Application` — class library
  - `src/CloudKnowledge.Infrastructure` — class library
  - `tests/CloudKnowledge.Tests` — xUnit
- Project references exactly as in §5:
  `Api → Infrastructure → Application`; `Tests → Api, Infrastructure, Application`.
  `Application` references no Azure package.
- `Directory.Build.props`: `net10.0`, `Nullable` enabled, `ImplicitUsings` enabled, `TreatWarningsAsErrors` true.
- `global.json` pinning the .NET 10 SDK (roll forward `latestFeature`).
- `.gitignore` for .NET (standard `dotnet new gitignore`) plus `.cache/`.
- `.editorconfig` (standard .NET conventions, file-scoped namespaces).
- `Program.cs` builds and runs an empty app (no endpoints yet besides what later tickets add).
- One smoke test in `CloudKnowledge.Tests` so `dotnet test` exercises the pipeline
  (e.g. `WebApplicationFactory<Program>` creates a client). Make `Program` visible to tests
  (`public partial class Program;`).

## Not in this ticket

- `CloudKnowledge.Eval` (created in M4).
- Any NuGet package beyond the templates and `Microsoft.AspNetCore.Mvc.Testing` — AI, VectorData, OpenAPI and
  Scalar packages come with the tickets that need them.
- Endpoints, options, DI extensions.

## Acceptance criteria

- [x] `dotnet build` succeeds with zero warnings.
- [x] `dotnet test` runs and passes the smoke test.
- [x] `dotnet run --project src/CloudKnowledge.Api` starts and listens.
- [x] Project reference graph matches §5 (no `Application → Infrastructure`, no `Application → Api`).
- [x] `.cache/` is git-ignored.
