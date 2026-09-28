# M2-03 — Document source and embedding cache

**Milestone:** M2 · **Blocked by:** M2-01 · **Spec:** §9 (cache), §12 (paths)

## Goal

Load the knowledge base from disk and compute embeddings only for texts that have not been embedded before —
restarts and re-indexing cost (almost) nothing.

## What to build

**Document source**
- `IDocumentSource` (Application): `Task<IReadOnlyList<MarkdownDocument>> LoadAsync(CancellationToken)`.
- `FileDocumentSource` (Infrastructure): reads all `*.md` in the resolved `DocsPath`, ordered by file name.
  A missing folder throws with a message naming the resolved path.

**Embedding cache**
- `IEmbeddingCache` (Application): `bool TryGet(string text, out ReadOnlyMemory<float> vector)`,
  `void Set(string text, ReadOnlyMemory<float> vector)`, `Task SaveAsync(CancellationToken)`.
- `JsonEmbeddingCache` (Infrastructure): key = SHA-256 hex of `"{EmbeddingDeployment}\n{text}"`; file at the
  resolved `EmbeddingCachePath`; loaded lazily on first use; directory created on save; saved atomically
  (write temp file, then move). An unreadable file is logged as a warning and treated as empty.
- `EmbeddingService` (Application): `Task<EmbeddingResult> EmbedAsync(IReadOnlyList<string> texts, CancellationToken)`
  - cached texts come from the cache,
  - all missing texts go to `IEmbeddingGenerator` in **one** `GenerateAsync` call,
  - new vectors are added to the cache and the cache is saved,
  - `EmbeddingResult(IReadOnlyList<ReadOnlyMemory<float>> Vectors, int FromCache, int EmbeddedNew)` keeps input order.

Register all three in `AddCloudKnowledge()`.

## Not in this ticket

- Vector store and indexing pipeline (M2-04).

## Acceptance criteria

- [ ] `FileDocumentSource` loads the nine docs in name order; missing folder → clear exception with the path.
- [ ] Cache: miss then hit; survives a new instance (file round-trip); a different `EmbeddingDeployment` yields a
      miss for the same text; corrupt file → empty cache, warning logged, no exception.
- [ ] `EmbeddingService`: first call embeds all (one generator call, `EmbeddedNew = n`); second call embeds none
      (`FromCache = n`, zero generator calls); mixed call only sends the missing texts; output order = input order.
- [ ] `.cache/` (or the configured cache file) is still git-ignored.
