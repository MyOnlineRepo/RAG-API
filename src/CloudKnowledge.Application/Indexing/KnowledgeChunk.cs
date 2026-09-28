using Microsoft.Extensions.VectorData;

namespace CloudKnowledge.Application.Indexing;

/// <summary>A chunk as stored in the vector collection <see cref="CollectionName"/> (SPEC §7).</summary>
public sealed class KnowledgeChunk
{
    public const string CollectionName = "knowledge";

    public const int EmbeddingDimensions = 1536; // text-embedding-3-small

    /// <summary>"{document}#{sectionSlug}-{partIndex}", e.g. "troubleshooting.md#http-503-after-deployment-0".</summary>
    [VectorStoreKey]
    public required string Id { get; set; }

    [VectorStoreData]
    public required string DocumentName { get; set; }

    [VectorStoreData]
    public required string Section { get; set; }

    /// <summary>The raw chunk text, without the title/section prefix used for embedding.</summary>
    [VectorStoreData]
    public required string Content { get; set; }

    [VectorStoreVector(EmbeddingDimensions, DistanceFunction = DistanceFunction.CosineSimilarity)]
    public ReadOnlyMemory<float> Embedding { get; set; }
}
