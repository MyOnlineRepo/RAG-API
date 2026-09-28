# M2-04 — Vector store and knowledge indexer

**Milestone:** M2 · **Blocked by:** M2-02, M2-03 · **Spec:** §6 (packages, decision 15), §7, §9 (decision 13)

## Goal

One call builds the complete searchable index: load → chunk → embed → store, with correct `IndexState` transitions.

## What to build

- Packages: `Microsoft.Extensions.VectorData.Abstractions` (Application), InMemory connector
  `Microsoft.SemanticKernel.Connectors.InMemory` (Infrastructure, **preview** — note the exact version in the commit).
- `KnowledgeChunk` record class (Application) with VectorData attributes as SPEC §7:
  key `Id`, data `DocumentName`, `Section`, `Content`, vector `Embedding` (1536, cosine similarity).
- Registration of the InMemory store and a `VectorStoreCollection<string, KnowledgeChunk>` named `knowledge`
  in `AddCloudKnowledge()`.
- `KnowledgeIndexer` (Application):
  - `Task<IndexingResult?> RunAsync(CancellationToken)`; returns `null` if a run is already active
    (non-blocking `SemaphoreSlim.WaitAsync(0)`).
  - `IndexState.MarkIndexing()` → delete + recreate the collection → load → chunk → `EmbeddingService` →
    upsert → `MarkReady(chunkCount)`.
  - On exception: `MarkFailed(message)`, log the exception, rethrow.
  - `IndexingResult(int Documents, int Chunks, int EmbeddedNew, int FromCache, TimeSpan Duration)`.
  - Structured log at the end: documents, chunks, new vs. cached embeddings, duration (SPEC §14).

## Not in this ticket

- Triggering on startup and the HTTP endpoint (M2-05). Searching (M2-06).

## Acceptance criteria

- [x] Using fakes: after `RunAsync`, the collection holds one record per chunk of the real `docs/`, and
      `IndexState` is `Ready` with that chunk count.
- [x] Second `RunAsync`: same chunk count, `EmbeddedNew = 0`, no duplicate records.
- [x] Failing embedding generator → state `Failed` with the error message; exception propagates.
- [x] While a run is active (blocking fake), a second `RunAsync` returns `null` immediately.
- [x] `Application` still references no Azure package (only the VectorData and M.E.AI abstractions).

## Implementation notes

- Packages: `Microsoft.Extensions.VectorData.Abstractions` 10.10.0 (Application),
  `Microsoft.SemanticKernel.Connectors.InMemory` **1.74.0-preview** (Infrastructure).
- `Application` also needs `Microsoft.Extensions.Logging.Abstractions` (10.0.12) for the structured log line
  (SPEC §14). Abstraction package, no Azure. A test checks the assembly references of `Application`
  (no `Azure*`, `OpenAI*`, `Microsoft.SemanticKernel*`).
- `KnowledgeChunk` uses `required` properties and constants `CollectionName = "knowledge"`,
  `EmbeddingDimensions = 1536`. Attribute names in VectorData 10.10: `VectorStoreKey`, `VectorStoreData`,
  `VectorStoreVector(1536, DistanceFunction = DistanceFunction.CosineSimilarity)` — as SPEC §7.
- Registration: `VectorStore` → `InMemoryVectorStore`, the collection via `GetCollection<string, KnowledgeChunk>`,
  `KnowledgeIndexer` — all singletons.
- A cancelled run is also recorded as `Failed` (with the cancellation message), so the state never stays `Indexing`.
- The indexer tests run through the test host: real `docs/`, bag-of-words fake, real InMemory store.
- **Deviation in an M1 test:** `Startup_fails_with_clear_message_for_invalid_configuration` failed intermittently
  (about 1 in 3 full runs) with `ObjectDisposedException` instead of `OptionsValidationException`. Cause: a race
  in `WebApplicationFactory`'s `DeferredHost` when the app throws during startup — it touches the already
  disposed service provider. It surfaced now because more tests run in parallel. The test now builds the
  service collection with `AddCloudKnowledge()` and calls `IStartupValidator.Validate()` — the exact check the
  host runs for `ValidateOnStart`. 20 consecutive full runs green. The real host was checked manually again:
  empty `AzureOpenAI:Endpoint` → `OptionsValidationException` with the clear message, exit code ≠ 0.
