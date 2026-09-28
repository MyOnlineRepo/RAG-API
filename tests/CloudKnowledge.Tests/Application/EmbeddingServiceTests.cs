using CloudKnowledge.Application.Indexing;
using CloudKnowledge.Tests.Fakes;

namespace CloudKnowledge.Tests.Application;

public sealed class EmbeddingServiceTests
{
    private readonly FakeEmbeddingGenerator _generator = FakeEmbeddingGenerator.BagOfWords();
    private readonly FakeEmbeddingCache _cache = new();

    private EmbeddingService Service => new(_generator, _cache);

    [Fact]
    public async Task First_call_embeds_all_texts_in_one_generator_call()
    {
        var result = await Service.EmbedAsync(["alpha", "beta", "gamma"], CancellationToken.None);

        Assert.Equal((0, 3), (result.FromCache, result.EmbeddedNew));
        Assert.Equal<string>(["alpha", "beta", "gamma"], Assert.Single(_generator.Calls));
        Assert.Equal(1, _cache.SaveCount);
    }

    [Fact]
    public async Task Second_call_uses_only_the_cache()
    {
        await Service.EmbedAsync(["alpha", "beta"], CancellationToken.None);

        var result = await Service.EmbedAsync(["alpha", "beta"], CancellationToken.None);

        Assert.Equal((2, 0), (result.FromCache, result.EmbeddedNew));
        Assert.Single(_generator.Calls);
        Assert.Equal(1, _cache.SaveCount);
    }

    [Fact]
    public async Task Mixed_call_sends_only_missing_texts_and_keeps_input_order()
    {
        await Service.EmbedAsync(["beta"], CancellationToken.None);

        var result = await Service.EmbedAsync(["alpha", "beta", "gamma"], CancellationToken.None);

        Assert.Equal((1, 2), (result.FromCache, result.EmbeddedNew));
        Assert.Equal<string>(["alpha", "gamma"], _generator.Calls[^1]);
        Assert.Equal(
            ["alpha", "beta", "gamma"],
            result.Vectors.Select(v => v.ToArray()).Select(v => new[] { "alpha", "beta", "gamma" }
                .Single(text => FakeEmbeddingGenerator.BagOfWordsVector(text).SequenceEqual(v))));
    }
}
