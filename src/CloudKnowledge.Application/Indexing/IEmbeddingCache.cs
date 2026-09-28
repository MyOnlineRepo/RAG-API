namespace CloudKnowledge.Application.Indexing;

/// <summary>Remembers embeddings of texts, so unchanged texts are never sent to Azure OpenAI twice.</summary>
public interface IEmbeddingCache
{
    bool TryGet(string text, out ReadOnlyMemory<float> vector);

    void Set(string text, ReadOnlyMemory<float> vector);

    Task SaveAsync(CancellationToken cancellationToken);
}
