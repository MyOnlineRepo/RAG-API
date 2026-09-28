using CloudKnowledge.Application.Configuration;
using CloudKnowledge.Application.Indexing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.VectorData;

namespace CloudKnowledge.Application.Search;

/// <param name="Score">Cosine similarity: 1 = same direction, higher = more similar.</param>
/// <param name="AboveThreshold"><c>Score &gt;= Rag:MinScore</c>.</param>
public sealed record SearchHit(string Document, string Section, double Score, bool AboveThreshold, string Content);

public sealed record SearchResult(string Query, double MinScore, IReadOnlyList<SearchHit> Results);

/// <summary>
/// Retrieval only, no LLM: embeds the query and returns the most similar chunks. Results are returned regardless of
/// <see cref="RagOptions.MinScore"/> (so the threshold can be calibrated) but each one is marked.
/// </summary>
public sealed class KnowledgeSearch(
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
    VectorStoreCollection<string, KnowledgeChunk> collection,
    IOptions<RagOptions> options)
{
    public async Task<SearchResult> SearchAsync(string query, int top, CancellationToken cancellationToken)
    {
        var minScore = options.Value.MinScore;

        // Queries are not cached: they are short, rarely repeat, and the cache is meant for the knowledge base.
        var queryVector = await embeddingGenerator.GenerateVectorAsync(query, cancellationToken: cancellationToken);

        var hits = new List<SearchHit>();
        await foreach (var result in collection.SearchAsync(queryVector, top, cancellationToken: cancellationToken))
        {
            var score = result.Score ?? 0;
            hits.Add(new SearchHit(result.Record.DocumentName, result.Record.Section, score, score >= minScore, result.Record.Content));
        }

        return new SearchResult(query, minScore, hits);
    }
}
