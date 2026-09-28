using CloudKnowledge.Application.Indexing;

namespace CloudKnowledge.Tests.Fakes;

/// <summary>In-memory cache that counts saves.</summary>
public sealed class FakeEmbeddingCache : IEmbeddingCache
{
    private readonly Dictionary<string, ReadOnlyMemory<float>> _entries = [];

    public int SaveCount { get; private set; }

    public bool TryGet(string text, out ReadOnlyMemory<float> vector) => _entries.TryGetValue(text, out vector);

    public void Set(string text, ReadOnlyMemory<float> vector) => _entries[text] = vector;

    public Task SaveAsync(CancellationToken cancellationToken)
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}
