# CloudKnowledge.Api — Specification

A small Retrieval Augmented Generation (RAG) demo built with ASP.NET Core and Azure OpenAI.
The API answers technical questions about a fictional SaaS system ("CloudStore") using **only**
a local Markdown knowledge base, and returns the sources it used.

This document is the single source of truth for scope. Anything not listed here is out of scope.

---

## 1. Goals

- Demonstrate RAG end to end: ingestion → chunking → embeddings → vector search → grounded generation.
- Make retrieval and generation separately observable (`/search` vs. `/ask`).
- Refuse to answer when the documentation does not contain the answer (two guard layers).
- Show which sources an answer is based on (inline citations).
- Measure retrieval quality with a small, reproducible evaluation.
- Use Azure OpenAI with Entra ID authentication (`DefaultAzureCredential`) — no API keys, no secrets in the repo.

## 2. Non-goals

- No frontend, no agents, no LangChain / LangGraph / Semantic Kernel orchestration.
- No deployment to Azure, no Dockerfile, no Bicep, no CI/CD pipeline. The API runs locally only.
- No authentication on the demo API itself.
- No persistent vector database, no Redis, no Cosmos DB, no message bus.
- No offline / fake mode for third parties; the author runs the demo.
- No LLM-as-judge evaluation of answer quality.

## 3. Audience and demo setting

The author presents the demo live (screen share or in person). Reviewers read the code and README on GitHub.
The README must therefore explain setup, architecture and results without requiring anyone to run it.

---

## 4. Knowledge base

### 4.1 Fictional system: CloudStore

A typical SaaS application, described only in documentation (never implemented):

```
Angular SPA ──HTTPS──► ASP.NET Core API ──► PostgreSQL (metadata, transactional data)
                                        └─► Azure Blob Storage (uploaded files)
Hosting:         Azure Container Apps (images from Azure Container Registry)
Authentication:  Microsoft Entra ID (Auth Code Flow + PKCE, JWT validation)
Service auth:    Managed Identity
Monitoring:      Application Insights, /health/live, /health/ready
CI/CD:           GitHub Actions → ACR → new Container App revision
```

### 4.2 Files

Location: `docs/` at repository root. Language: **English**. 9 files, roughly 500–1,000 words each,
structured with H2 sections (the chunker depends on this).

| File | Content |
|---|---|
| `architecture.md` | Components, data flow, stateless API, where data lives |
| `authentication.md` | Entra ID, Auth Code Flow + PKCE, JWT validation in the API |
| `security.md` | Managed Identity for Blob Storage / PostgreSQL / Key Vault, no stored credentials, RBAC roles |
| `api.md` | Main REST endpoints of CloudStore, versioning, error format |
| `deployment.md` | Push to main → restore/test/publish → Docker build → ACR → new revision |
| `container-apps.md` | Revisions, scaling rules, ingress, environment variables, probes |
| `storage.md` | Upload flow, containers, naming, metadata in PostgreSQL |
| `monitoring.md` | Application Insights, structured logs, `GET /health/live`, `GET /health/ready` |
| `troubleshooting.md` | e.g. "HTTP 503 after deployment", "401 from API", "Uploads fail" |

Claude drafts the files; the author reviews and owns the content.

### 4.3 Deliberate test cases (must hold after every edit to `docs/`)

**Facts split across files** (retrieval must find ≥ 2 chunks):
- Uploads stored in Blob Storage (`storage.md`) + Blob access via Managed Identity (`security.md`).
- HTTP 503 checks (`troubleshooting.md`) + health endpoint paths (`monitoring.md`) + revision status (`container-apps.md`).
- Push to main (`deployment.md`) + new revision behaviour (`container-apps.md`).

**Deliberately missing facts** (must not appear anywhere, not even in passing):
- Maximum upload file size.
- SLA / availability target.
- Backup retention period.

**Bait:** `storage.md` discusses uploads in detail but never mentions a size limit — tests whether the model
fills the gap with general Azure Blob Storage knowledge.

---

## 5. Solution structure

```
CloudKnowledge.sln
src/
  CloudKnowledge.Api/              Minimal API endpoints, Program.cs, hosted indexing service, health checks
  CloudKnowledge.Application/      RagService, MarkdownChunker, PromptBuilder, CitationParser,
                                   domain models, abstractions (IDocumentSource, IEmbeddingCache, ...)
  CloudKnowledge.Infrastructure/   Azure OpenAI wiring, VectorData InMemory collection, file-system
                                   document source, JSON embedding cache, AddCloudKnowledge(...) DI extension
  CloudKnowledge.Eval/             Console app: retrieval evaluation (references Application + Infrastructure)
tests/
  CloudKnowledge.Tests/            xUnit unit + integration tests
docs/                              Knowledge base (CloudStore)
eval/questions.json                Evaluation set
```

Dependency direction: `Api → Infrastructure → Application`, `Eval → Infrastructure → Application`.
`Application` has no Azure dependencies; it depends only on `Microsoft.Extensions.AI.Abstractions`
and `Microsoft.Extensions.VectorData.Abstractions`.

DI registration lives in `Infrastructure` as `services.AddCloudKnowledge(configuration)` so API and Eval share it.

## 6. Technology

| Area | Choice |
|---|---|
| Runtime | .NET 10 (LTS) |
| API style | Minimal APIs with endpoint groups; OpenAPI via `Microsoft.AspNetCore.OpenApi`, Scalar UI in Development |
| AI abstraction | `Microsoft.Extensions.AI` — `IChatClient`, `IEmbeddingGenerator<string, Embedding<float>>` |
| Azure client | `Azure.AI.OpenAI` `AzureOpenAIClient` + `Azure.Identity` `DefaultAzureCredential` |
| Vector store | `Microsoft.Extensions.VectorData` with the InMemory connector |
| Embedding model | `text-embedding-3-small` (1536 dimensions) |
| Chat model | a small, inexpensive chat model; the deployment name is configuration only |
| Tests | xUnit, `Microsoft.AspNetCore.Mvc.Testing` (`WebApplicationFactory`) |

**Verify in ticket 1:** the current package name and preview status of the VectorData InMemory connector.
It ships from the Semantic Kernel repository/namespace. Talking point: only the connector is used,
not Semantic Kernel's orchestration.

---

## 7. Data model

```csharp
// Stored in the vector collection
public sealed class KnowledgeChunk
{
    [VectorStoreKey]    public string Id { get; set; }            // "{document}#{sectionSlug}-{partIndex}"
    [VectorStoreData]   public string DocumentName { get; set; }  // "troubleshooting.md"
    [VectorStoreData]   public string Section { get; set; }       // "HTTP 503 after deployment"
    [VectorStoreData]   public string Content { get; set; }       // raw chunk text (without prefix)
    [VectorStoreVector(1536, DistanceFunction = DistanceFunction.CosineSimilarity)]
                        public ReadOnlyMemory<float> Embedding { get; set; }
}
```

Attribute names are illustrative; use whatever the current VectorData version provides.

## 8. Chunking

`MarkdownChunker` (pure function, no I/O):

1. Split each document at H2 (`## `) headings. Text before the first H2 becomes section "Introduction"
   (title taken from the H1, or the file name if there is no H1).
2. A section ≤ `Chunking:MaxTokens` (default 400) becomes exactly one chunk.
3. A longer section is split at paragraph boundaries (blank lines) into parts ≤ `MaxTokens`.
   Code blocks and lists are never split in the middle. A single paragraph larger than `MaxTokens` stays whole.
4. No overlap.
5. Token count is approximated (e.g. `chars / 4`); an exact tokenizer is not required.
6. **Text that gets embedded** = `"{DocumentTitle} > {Section}\n\n{Content}"`. The prefix is used only for
   embedding; `Content` stores the raw text.

## 9. Indexing

- `KnowledgeIndexingService` (`IHostedService` / `BackgroundService`) indexes all `docs/*.md` on startup.
- Pipeline: load → chunk → embed (via cache) → upsert into the collection.
- Re-index replaces the collection's contents fully (delete + upsert, or new collection and swap).
- **Embedding cache:** JSON file (path configurable, default `.cache/embeddings.json`, git-ignored).
  Key = SHA-256 of `"{EmbeddingDeployment}\n{textToEmbed}"`. Only missing entries are sent to Azure.
  Changing the embedding deployment therefore invalidates the cache automatically.
- Readiness: an `IndexState` singleton (`NotStarted | Indexing | Ready | Failed`, chunk count, last indexed at).

## 10. RAG flow (`/ask`)

```
question
  → embed question
  → vector search (TopK)
  → drop results with score < MinScore
  → none left?  → return answered=false, fixed message, no LLM call
  → build prompt with numbered context [1..n]
  → chat completion
  → parse [n] markers → citations
  → response
```

### 10.1 Prompt

System message (content, not exact wording):

- You are a technical support assistant for the CloudStore application.
- Answer **only** using the provided context. Do not use general knowledge.
- If the context does not contain the answer, say that the documentation does not contain this information.
  Do not guess.
- Cite every statement with the number of the context entry, e.g. `[1]` or `[2][3]`.
- Answer in the language of the question.

User message:

```
Context:

[1] Source: troubleshooting.md — HTTP 503 after deployment
<content>

[2] Source: monitoring.md — Health endpoints
<content>

Question:
<question>
```

`PromptBuilder` is a pure function and unit-tested.

### 10.2 Citation parsing

`CitationParser` extracts all `[n]` markers (also `[1][3]` and `[1, 3]`), deduplicates them, ignores numbers
outside `1..n`, and maps them to the retrieved sources. If there are no markers, `citations` is empty
(logged as a warning, answer is still returned).

---

## 11. HTTP API

Base path `/api/knowledge`. JSON, camelCase.

### `GET /api/knowledge/search?q={text}&top={k}`

Retrieval only, no LLM. `top` defaults to `Rag:TopK`, max 20. Returns results **regardless** of `MinScore`
(so thresholds can be calibrated), but marks each one.

```json
{
  "query": "How are uploaded files stored?",
  "minScore": 0.45,
  "results": [
    { "document": "storage.md", "section": "Upload flow", "score": 0.62, "aboveThreshold": true, "content": "..." }
  ]
}
```

### `POST /api/knowledge/ask`

Request: `{ "question": "..." }` (required, 3–1000 characters, otherwise 400 ProblemDetails).

```json
{
  "answered": true,
  "answer": "After a push to main the pipeline restores and tests the solution [1] ... a new revision is created [2].",
  "citations": [
    { "number": 1, "document": "deployment.md", "section": "Pipeline stages", "score": 0.58 }
  ],
  "retrievedSources": [
    { "number": 1, "document": "deployment.md", "section": "Pipeline stages", "score": 0.58 },
    { "number": 2, "document": "container-apps.md", "section": "Revisions", "score": 0.51 }
  ]
}
```

When no result reaches `MinScore`:

```json
{
  "answered": false,
  "answer": "The documentation does not contain information to answer this question.",
  "citations": [],
  "retrievedSources": []
}
```

`answered` is `false` only for the threshold guard. When the LLM itself declines, `answered` is `true`
and the refusal is in `answer` (it cannot be detected reliably; do not try).

### `POST /api/knowledge/index`

Re-indexes `docs/`. Returns `200 { "documents": 9, "chunks": 47, "embeddedNew": 3, "fromCache": 44, "durationMs": 812 }`.
Returns `409` if indexing is already running. **No authentication** — local demo only; documented in the README.

### Health

- `GET /health/live` — always healthy once the process runs.
- `GET /health/ready` — healthy only when `IndexState == Ready`.
- `/ask` and `/search` return `503` ProblemDetails while the index is not ready.

## 12. Configuration

```json
{
  "AzureOpenAI": {
    "Endpoint": "https://<resource>.openai.azure.com/",
    "ChatDeployment": "<chat-deployment>",
    "EmbeddingDeployment": "text-embedding-3-small"
  },
  "Rag": { "TopK": 5, "MinScore": 0.0 },
  "Chunking": { "MaxTokens": 400 },
  "Knowledge": { "DocsPath": "docs", "EmbeddingCachePath": ".cache/embeddings.json" }
}
```

- Options pattern with data annotations and `ValidateOnStart`.
- The endpoint and deployment names go in `appsettings.Development.json` / user secrets / env vars;
  the committed `appsettings.json` contains placeholders only. No keys anywhere.
- `MinScore` starts as a placeholder and is **set from the evaluation results** (ticket: eval).

## 13. Authentication to Azure OpenAI

`DefaultAzureCredential` → Entra ID → Azure OpenAI. Locally this resolves to `az login` / Visual Studio credentials.
The signed-in user needs the role **Cognitive Services OpenAI User** on the Azure OpenAI resource
(the README must mention this; without it calls fail with 401 despite a successful login).
The README explains that in Azure the same code would use a Managed Identity with the same role assignment —
no code change.

## 14. Logging

Structured `ILogger` messages (no string interpolation):

- Indexing: documents, chunks, new vs. cached embeddings, duration.
- `/ask`: question length, top scores, threshold triggered yes/no, number of citations, retrieval and LLM latency.
- Warning when an answer contains no citation markers.
- Question text at `Debug` level only.

## 15. Evaluation (`CloudKnowledge.Eval`)

- Input: `eval/questions.json`, 12–15 answerable + 3 unanswerable questions (derived from §4.3):

```json
[
  { "question": "Where are uploaded files stored and how does the app authenticate there?",
    "expected": [ { "document": "storage.md", "section": "Upload flow" },
                  { "document": "security.md", "section": "Managed Identity" } ] },
  { "question": "What is the maximum upload size?", "expected": [] }
]
```

- Runs retrieval only (no chat calls), using the same services and configuration as the API.
- Metrics:
  - **Recall@k** for answerable questions: share of expected sections found in the top k (k = 1, 3, 5).
  - **Hit rate**: share of answerable questions with at least one expected section in the top k.
  - **Rejection rate**: share of unanswerable questions whose best score is below `MinScore`.
  - **Score distribution**: best score per question, to choose `MinScore` between the answerable and unanswerable groups.
- Output: console table + Markdown table (`eval/results.md`) for the README.
- Exit code 0 always (it is a report, not a gate).

## 16. Tests (`CloudKnowledge.Tests`)

No test calls Azure. AI dependencies are replaced by fakes (a deterministic fake `IEmbeddingGenerator`
and a scripted fake `IChatClient`).

Unit tests (minimum):
- `MarkdownChunker`: H2 splitting, intro section, long section split at paragraphs, code blocks never split, prefix format, stable IDs.
- `CitationParser`: `[1]`, `[1][3]`, `[1, 3]`, out-of-range, duplicates, no markers.
- `PromptBuilder`: numbering, sources and sections in the context, question placement.
- `RagService`: no LLM call when all scores < `MinScore`; below-threshold chunks excluded from the prompt; citations mapped correctly.
- Embedding cache: hit/miss, key changes with the deployment name.

Integration tests (`WebApplicationFactory` with fakes):
- `/health/ready` is unhealthy before indexing and healthy after.
- `/ask` returns 400 for invalid input and the documented response shape for valid input.
- `/search` returns results with `aboveThreshold` flags.

## 17. README (deliverable, not an afterthought)

1. What it is (3 sentences) and the architecture diagram.
2. Key concepts demonstrated.
3. Setup: prerequisites, Azure OpenAI deployments, role assignment, `az login`, configuration, `dotnet run`.
4. Demo script (§18).
5. Design decisions (§20) in short form — these are the interview talking points.
6. Evaluation results table and how `MinScore` was chosen.
7. Limitations and next steps (§19).

## 18. Demo script (acceptance scenario)

1. Start the API → `/health/ready` switches to healthy once indexing is finished (log shows cache hits).
2. `GET /search?q=How does the deployment process work?` → `deployment.md`, `container-apps.md` at the top.
3. `POST /ask` "What happens after a new version is pushed to main?" → multi-step answer with citations from ≥ 2 files.
4. `POST /ask` "Nach dem Deployment liefert die Anwendung HTTP 503. Was soll ich prüfen?" → German answer
   from English docs, cites `troubleshooting.md` and `monitoring.md`.
5. `POST /ask` "Where are uploads stored and how does the application authenticate there?" → cites `storage.md` + `security.md`.
6. `POST /ask` "What is the maximum supported file size?" → `answered: false` (threshold) or an explicit
   "not documented" answer — never a number.

The project is done when all six steps work against a real Azure OpenAI resource and all tests pass.

## 19. Backlog (explicitly not in the MVP)

- Second chunking strategy (fixed size + overlap) behind `IChunkingStrategy`, compared via the evaluation.
- Azure AI Search as the vector store; vector vs. hybrid search comparison.
- LLM-as-judge answer evaluation.
- Container image + Container Apps deployment with Managed Identity.

## 20. Decision log

| # | Decision | Alternatives rejected | Reason |
|---|---|---|---|
| 1 | Author presents the demo; no third-party run mode | Offline/fake mode; public instance | Avoids building a second infrastructure path and running costs |
| 2 | Local only; Managed Identity explained, not deployed | Container Apps + Bicep; plus CI/CD | Keeps scope to a weekend |
| 3 | VectorData + InMemory connector | Own cosine store | Swappable abstraction as a talking point |
| 4 | Chunk per H2 section, split long ones at paragraphs, heading prefix, no overlap | Fixed 500 tokens + overlap | Documents are too short for fixed-size chunks; sections give precise hits and clean citations |
| 5 | Prompt + `MinScore` threshold, no LLM call below it | Prompt only; structured JSON output | Deterministic + probabilistic guard, cheap to build |
| 6 | Inline `[n]` markers → `citations` + `retrievedSources` | Retrieved only; JSON output | Honest about "retrieved ≠ used" without fragile parsing |
| 7 | Retrieval evaluation as a separate console app | None; LLM-as-judge | Proves retrieval quality and calibrates `MinScore` |
| 8 | English docs, 9 files, deliberate split/missing/bait facts | — | Enables cross-language demo and hallucination test |
| 9 | Separate Api / Application / Infrastructure projects | Folders in one project; single Core library | Author's choice; DI extension shared by Api and Eval |
| 10 | Index on startup, readiness gate, hash-keyed embedding cache incl. model name | No cache; manual only | Fast restarts, no repeated cost, mirrors CloudStore's own health model |
| 11 | .NET 10, Minimal APIs, M.E.AI, `DefaultAzureCredential`, `text-embedding-3-small`, TopK 5 | — | Current, idiomatic defaults |
