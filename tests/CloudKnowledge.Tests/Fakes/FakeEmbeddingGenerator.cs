using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;

namespace CloudKnowledge.Tests.Fakes;

/// <summary>Returns deterministic embeddings produced by <paramref name="embed"/> and records every input.</summary>
public sealed class FakeEmbeddingGenerator(Func<string, float[]> embed) : IEmbeddingGenerator<string, Embedding<float>>
{
    public const int Dimensions = 1536;

    public List<string> Inputs { get; } = [];

    /// <summary>One entry per <see cref="GenerateAsync"/> call with the texts of that call.</summary>
    public List<IReadOnlyList<string>> Calls { get; } = [];

    /// <summary>
    /// Bag-of-words embedding: every word is hashed into one of 1536 dimensions, the vector is L2-normalised.
    /// Deterministic, and texts that share words get a high cosine similarity — enough for realistic search tests.
    /// </summary>
    public static FakeEmbeddingGenerator BagOfWords() => new(BagOfWordsVector);

    public static float[] BagOfWordsVector(string text)
    {
        var vector = new float[Dimensions];
        foreach (var word in Regex.Split(text.ToLowerInvariant(), @"[^\p{L}\p{N}]+").Where(w => w.Length > 0))
        {
            vector[StableHash(word) % Dimensions] += 1f;
        }

        var length = MathF.Sqrt(vector.Sum(v => v * v));
        if (length > 0)
        {
            for (var i = 0; i < vector.Length; i++)
            {
                vector[i] /= length;
            }
        }

        return vector;
    }

    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values, EmbeddingGenerationOptions? options = null, CancellationToken cancellationToken = default)
    {
        var batch = values.ToList();
        Calls.Add(batch);

        var embeddings = new GeneratedEmbeddings<Embedding<float>>();
        foreach (var value in batch)
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

    // FNV-1a: string.GetHashCode() is randomised per process, this is not.
    private static uint StableHash(string word)
    {
        var hash = 2166136261u;
        foreach (var c in word)
        {
            hash = (hash ^ c) * 16777619u;
        }

        return hash;
    }
}
