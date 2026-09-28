using CloudKnowledge.Application.Chunking;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.VectorData;

namespace CloudKnowledge.Application.Indexing;

public sealed record IndexingResult(int Documents, int Chunks, int EmbeddedNew, int FromCache, TimeSpan Duration);

/// <summary>
/// Builds the searchable index: load → chunk → embed (via cache) → store. Every run rebuilds the collection from
/// scratch and moves <see cref="IndexState"/> through Indexing → Ready (or Failed). Only one run at a time.
/// </summary>
public sealed class KnowledgeIndexer(
    IDocumentSource documentSource,
    MarkdownChunker chunker,
    EmbeddingService embeddingService,
    VectorStoreCollection<string, KnowledgeChunk> collection,
    IndexState indexState,
    TimeProvider timeProvider,
    ILogger<KnowledgeIndexer> logger)
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <returns>The run's statistics, or <c>null</c> if another run is already active.</returns>
    public async Task<IndexingResult?> RunAsync(CancellationToken cancellationToken)
    {
        if (!await _gate.WaitAsync(0, cancellationToken))
        {
            return null;
        }

        try
        {
            var started = timeProvider.GetTimestamp();
            indexState.MarkIndexing();

            try
            {
                await collection.EnsureCollectionDeletedAsync(cancellationToken);
                await collection.EnsureCollectionExistsAsync(cancellationToken);

                var documents = await documentSource.LoadAsync(cancellationToken);
                var chunks = documents.SelectMany(chunker.Chunk).ToList();
                var embeddings = await embeddingService.EmbedAsync(chunks.Select(c => c.TextToEmbed).ToList(), cancellationToken);

                await collection.UpsertAsync(chunks.Select((chunk, i) => new KnowledgeChunk
                {
                    Id = chunk.Id,
                    DocumentName = chunk.DocumentName,
                    Section = chunk.Section,
                    Content = chunk.Content,
                    Embedding = embeddings.Vectors[i],
                }), cancellationToken);

                var result = new IndexingResult(
                    documents.Count, chunks.Count, embeddings.EmbeddedNew, embeddings.FromCache, timeProvider.GetElapsedTime(started));
                indexState.MarkReady(chunks.Count);

                logger.LogInformation(
                    "Indexed {Documents} documents into {Chunks} chunks ({EmbeddedNew} new embeddings, {FromCache} from cache) in {DurationMs} ms",
                    result.Documents, result.Chunks, result.EmbeddedNew, result.FromCache, (long)result.Duration.TotalMilliseconds);
                return result;
            }
            catch (Exception exception)
            {
                indexState.MarkFailed(exception.Message);
                logger.LogError(exception, "Indexing failed");
                throw;
            }
        }
        finally
        {
            _gate.Release();
        }
    }
}
