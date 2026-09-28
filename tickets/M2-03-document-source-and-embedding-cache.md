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

- [x] `FileDocumentSource` loads the nine docs in name order; missing folder → clear exception with the path.
- [x] Cache: miss then hit; survives a new instance (file round-trip); a different `EmbeddingDeployment` yields a
      miss for the same text; corrupt file → empty cache, warning logged, no exception.
- [x] `EmbeddingService`: first call embeds all (one generator call, `EmbeddedNew = n`); second call embeds none
      (`FromCache = n`, zero generator calls); mixed call only sends the missing texts; output order = input order.
- [x] `.cache/` (or the configured cache file) is still git-ignored.

## Implementation notes

- `IDocumentSource`, `IEmbeddingCache` and `EmbeddingService` live in `Application/Indexing` next to `IndexState`;
  `FileDocumentSource`, `JsonEmbeddingCache` in `Infrastructure/Knowledge` next to `KnowledgePaths`.
- Cache file format: a JSON object `{ "<sha256 hex>": [floats…] }` — readable, ~1–2 MB for the knowledge base.
- "Unreadable" is interpreted as invalid JSON (`JsonException`) → warning + empty cache. I/O errors such as
  missing permissions still throw, because hiding them would recompute everything silently.
- Missing texts are not de-duplicated: the knowledge base produces unique `TextToEmbed` values anyway.
  `FromCache + EmbeddedNew` always equals the number of input texts.
- The cache is saved only when something new was embedded.
- **Test host addition:** `CloudKnowledgeApiFactory` gives every instance its own temp cache file
  (`EmbeddingCachePath`, deleted on dispose), so tests never see embeddings from earlier runs once indexing
  runs on startup (M2-05). `FakeEmbeddingGenerator.Calls` records each generator call; new fakes
  `FakeEmbeddingCache` and `ListLogger<T>`.
- Consequence of decision 12: with the default relative path the cache lives in the build output
  (`bin/…/.cache/embeddings.json`). It is ignored by git via `bin/` and `.cache/` (checked with `git check-ignore`).
