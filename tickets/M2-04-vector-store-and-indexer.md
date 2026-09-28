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

- [ ] Using fakes: after `RunAsync`, the collection holds one record per chunk of the real `docs/`, and
      `IndexState` is `Ready` with that chunk count.
- [ ] Second `RunAsync`: same chunk count, `EmbeddedNew = 0`, no duplicate records.
- [ ] Failing embedding generator → state `Failed` with the error message; exception propagates.
- [ ] While a run is active (blocking fake), a second `RunAsync` returns `null` immediately.
- [ ] `Application` still references no Azure package (only the VectorData and M.E.AI abstractions).
