# M1-02 — Configuration and Azure OpenAI wiring

**Milestone:** M1 · **Blocked by:** M1-01 · **Spec:** §5, §6, §12, §13

## Goal

All configuration is typed, validated at startup, and the AI clients are registered in DI via
`DefaultAzureCredential` — so M2 can inject `IEmbeddingGenerator` and M3 `IChatClient` without touching setup code.

## What to build

**Options (in `Application`, plain classes, no Azure types):**

| Class | Section | Properties and validation |
|---|---|---|
| `AzureOpenAIOptions` | `AzureOpenAI` | `Endpoint` (required, absolute https URI), `ChatDeployment` (required), `EmbeddingDeployment` (required) |
| `RagOptions` | `Rag` | `TopK` (1–20, default 5), `MinScore` (0.0–1.0, default 0.0) |
| `ChunkingOptions` | `Chunking` | `MaxTokens` (50–2000, default 400) |
| `KnowledgeOptions` | `Knowledge` | `DocsPath` (required, default `docs`), `EmbeddingCachePath` (required, default `.cache/embeddings.json`) |

- Data annotations + `ValidateDataAnnotations()` + `ValidateOnStart()`.
- `appsettings.json` contains every section with **placeholders** for endpoint and deployment names.
- `appsettings.Development.json` is not used for real values — real values go into user secrets
  (`UserSecretsId` on the Api project) or environment variables.

**DI extension (in `Infrastructure`):** `IServiceCollection AddCloudKnowledge(this IServiceCollection, IConfiguration)`

- Binds and validates all options above.
- Registers `AzureOpenAIClient` as singleton with `new DefaultAzureCredential()` and `AzureOpenAIOptions.Endpoint`.
- Registers `IChatClient` (deployment `ChatDeployment`) and
  `IEmbeddingGenerator<string, Embedding<float>>` (deployment `EmbeddingDeployment`)
  using the `Microsoft.Extensions.AI.OpenAI` adapters (`AsIChatClient()` / `AsIEmbeddingGenerator()`).
- Registration is lazy: no network call at startup.
- `Program.cs` calls `builder.Services.AddCloudKnowledge(builder.Configuration)`.

**Packages:** `Azure.AI.OpenAI`, `Azure.Identity`, `Microsoft.Extensions.AI`, `Microsoft.Extensions.AI.OpenAI`
in `Infrastructure`; `Microsoft.Extensions.AI.Abstractions` in `Application`. Use current stable versions
(prerelease only where no stable exists — note it in the commit message).

## Not in this ticket

- Any actual call to Azure OpenAI (first real call happens in M2 indexing).
- API keys or key-based auth in any form.
- OpenAPI / Scalar (M2, when there is a first endpoint worth documenting).

## Acceptance criteria

- [ ] App fails to start with a clear validation message when e.g. `AzureOpenAI:Endpoint` is missing or not https.
- [ ] App starts with valid (even fake) values, without network access.
- [ ] Test: with valid in-memory config, the service provider resolves `IChatClient` and `IEmbeddingGenerator<string, Embedding<float>>`.
- [ ] Test: with an invalid `Rag:TopK` (e.g. 0), startup validation fails.
- [ ] Tests can replace `IChatClient` / `IEmbeddingGenerator` via `WebApplicationFactory.ConfigureTestServices` (proves M3/M4 test strategy works).
- [ ] `git grep -i "apikey\|api-key"` finds nothing outside of docs/README prose.
