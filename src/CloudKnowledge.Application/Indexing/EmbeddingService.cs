using Microsoft.Extensions.AI;

namespace CloudKnowledge.Application.Indexing;

/// <param name="Vectors">One vector per input text, in input order.</param>
public sealed record EmbeddingResult(IReadOnlyList<ReadOnlyMemory<float>> Vectors, int FromCache, int EmbeddedNew);

/// <summary>
/// Embeds texts through the cache: cached texts are taken from <see cref="IEmbeddingCache"/>, all missing texts go
/// to the embedding generator in one call and are then added to the cache.
/// </summary>
public sealed class EmbeddingService(IEmbeddingGenerator<string, Embedding<float>> generator, IEmbeddingCache cache)
{
    public async Task<EmbeddingResult> EmbedAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken)
    {
        var vectors = new ReadOnlyMemory<float>[texts.Count];
        var missing = new List<int>();

        for (var i = 0; i < texts.Count; i++)
        {
            if (cache.TryGet(texts[i], out var cached))
            {
                vectors[i] = cached;
            }
            else
            {
                missing.Add(i);
            }
        }

        if (missing.Count > 0)
        {
            var embeddings = await generator.GenerateAsync(missing.Select(i => texts[i]), cancellationToken: cancellationToken);
            if (embeddings.Count != missing.Count)
            {
                throw new InvalidOperationException(
                    $"The embedding generator returned {embeddings.Count} vectors for {missing.Count} texts.");
            }

            for (var j = 0; j < missing.Count; j++)
            {
                vectors[missing[j]] = embeddings[j].Vector;
                cache.Set(texts[missing[j]], embeddings[j].Vector);
            }

            await cache.SaveAsync(cancellationToken);
        }

        return new EmbeddingResult(vectors, FromCache: texts.Count - missing.Count, EmbeddedNew: missing.Count);
    }
}
