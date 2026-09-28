namespace CloudKnowledge.Tests.Fakes;

public sealed class FakeEmbeddingGeneratorTests
{
    [Fact]
    public async Task Bag_of_words_returns_deterministic_unit_vectors_with_1536_dimensions()
    {
        using var generator = FakeEmbeddingGenerator.BagOfWords();

        var embeddings = await generator.GenerateAsync(["HTTP 503 after deployment", "HTTP 503 after deployment"]);
        var first = embeddings[0].Vector.ToArray();

        Assert.Equal(1536, first.Length);
        Assert.Equal(1.0, Math.Sqrt(first.Sum(v => v * v)), precision: 5);
        Assert.Equal(first, embeddings[1].Vector.ToArray());
    }

    [Fact]
    public void Bag_of_words_puts_texts_with_shared_words_closer_together()
    {
        var query = FakeEmbeddingGenerator.BagOfWordsVector("HTTP 503 after deployment");

        var related = Cosine(query, FakeEmbeddingGenerator.BagOfWordsVector("503 deployment"));
        var unrelated = Cosine(query, FakeEmbeddingGenerator.BagOfWordsVector("upload blob storage"));

        Assert.True(related > unrelated, $"related {related} should be greater than unrelated {unrelated}");
    }

    private static double Cosine(float[] a, float[] b) => a.Zip(b, (x, y) => (double)x * y).Sum();
}
