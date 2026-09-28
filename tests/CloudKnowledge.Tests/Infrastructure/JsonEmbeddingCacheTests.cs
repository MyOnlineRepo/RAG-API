using CloudKnowledge.Application.Configuration;
using CloudKnowledge.Infrastructure.Knowledge;
using CloudKnowledge.Tests.Fakes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CloudKnowledge.Tests.Infrastructure;

public sealed class JsonEmbeddingCacheTests : IDisposable
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("cloudknowledge-cache-");

    // A sub folder that does not exist yet: saving must create it.
    private string CachePath => Path.Combine(_directory.FullName, "nested", "embeddings.json");

    private JsonEmbeddingCache Cache(string deployment = "text-embedding-3-small", ListLogger<JsonEmbeddingCache>? logger = null) =>
        new(Options.Create(new KnowledgeOptions { EmbeddingCachePath = CachePath }),
            Options.Create(new AzureOpenAIOptions { EmbeddingDeployment = deployment }),
            logger ?? new ListLogger<JsonEmbeddingCache>());

    [Fact]
    public void Miss_then_hit()
    {
        var cache = Cache();

        Assert.False(cache.TryGet("hello", out _));
        cache.Set("hello", new[] { 1f, 2f });

        Assert.True(cache.TryGet("hello", out var vector));
        Assert.Equal([1f, 2f], vector.ToArray());
    }

    [Fact]
    public async Task Survives_a_new_instance_through_the_file()
    {
        var first = Cache();
        first.Set("hello", new[] { 0.5f, -0.25f });
        await first.SaveAsync(CancellationToken.None);

        Assert.True(Cache().TryGet("hello", out var vector));
        Assert.Equal([0.5f, -0.25f], vector.ToArray());
        Assert.False(File.Exists(CachePath + ".tmp"));
    }

    [Fact]
    public async Task Different_embedding_deployment_is_a_miss()
    {
        var first = Cache("text-embedding-3-small");
        first.Set("hello", new[] { 1f });
        await first.SaveAsync(CancellationToken.None);

        Assert.False(Cache("text-embedding-3-large").TryGet("hello", out _));
    }

    [Fact]
    public void Corrupt_file_is_an_empty_cache_with_a_warning()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(CachePath)!);
        File.WriteAllText(CachePath, "{ not json");
        var logger = new ListLogger<JsonEmbeddingCache>();

        var found = Cache(logger: logger).TryGet("hello", out _);

        Assert.False(found);
        var warning = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, warning.Level);
        Assert.Contains(CachePath, warning.Message);
    }

    public void Dispose() => _directory.Delete(recursive: true);
}
