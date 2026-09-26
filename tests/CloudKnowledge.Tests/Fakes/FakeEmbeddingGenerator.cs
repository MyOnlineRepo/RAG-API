using Microsoft.Extensions.AI;

namespace CloudKnowledge.Tests.Fakes;

/// <summary>Returns deterministic embeddings produced by <paramref name="embed"/> and records every input.</summary>
public sealed class FakeEmbeddingGenerator(Func<string, float[]> embed) : IEmbeddingGenerator<string, Embedding<float>>
{
    public List<string> Inputs { get; } = [];

    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values, EmbeddingGenerationOptions? options = null, CancellationToken cancellationToken = default)
    {
        var embeddings = new GeneratedEmbeddings<Embedding<float>>();
        foreach (var value in values)
        {
            Inputs.Add(value);
            embeddings.Add(new Embedding<float>(embed(value)));
        }

        return Task.FromResult(embeddings);
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}
