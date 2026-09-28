using CloudKnowledge.Application.Chunking;
using CloudKnowledge.Application.Indexing;
using CloudKnowledge.Tests.Fakes;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.VectorData;

namespace CloudKnowledge.Tests.Application;

public sealed class KnowledgeIndexerTests : IDisposable
{
    // No indexing on startup: each test controls the runs itself.
    private readonly CloudKnowledgeApiFactory _factory =
        new CloudKnowledgeApiFactory().WithSetting("Knowledge:IndexOnStartup", "false");

    private static readonly int ExpectedChunks = CountChunksOfRealDocs();

    [Fact]
    public async Task Run_stores_one_record_per_chunk_and_marks_the_index_ready()
    {
        var result = await Indexer(_factory).RunAsync(CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal((9, ExpectedChunks, ExpectedChunks, 0), (result.Documents, result.Chunks, result.EmbeddedNew, result.FromCache));
        Assert.Equal(ExpectedChunks, (await StoredRecords(_factory)).Count);
        var state = _factory.Services.GetRequiredService<IndexState>().Current;
        Assert.Equal((IndexStatus.Ready, ExpectedChunks), (state.Status, state.ChunkCount));
    }

    [Fact]
    public async Task Stored_record_carries_the_chunk_data_and_its_embedding()
    {
        await Indexer(_factory).RunAsync(CancellationToken.None);

        var record = (await StoredRecords(_factory)).Single(r => r.Id == "troubleshooting.md#http-503-after-deployment-0");

        Assert.Equal(("troubleshooting.md", "HTTP 503 after deployment"), (record.DocumentName, record.Section));
        Assert.DoesNotContain(" > HTTP 503 after deployment", record.Content);
        Assert.Equal(KnowledgeChunk.EmbeddingDimensions, record.Embedding.Length);
    }

    [Fact]
    public async Task Second_run_uses_the_cache_and_creates_no_duplicates()
    {
        var indexer = Indexer(_factory);
        await indexer.RunAsync(CancellationToken.None);

        var second = await indexer.RunAsync(CancellationToken.None);

        Assert.NotNull(second);
        Assert.Equal((ExpectedChunks, 0, ExpectedChunks), (second.Chunks, second.EmbeddedNew, second.FromCache));
        Assert.Equal(ExpectedChunks, (await StoredRecords(_factory)).Count);
        Assert.Single(_factory.EmbeddingGenerator.Calls);
    }

    [Fact]
    public async Task Failing_embedding_generator_marks_the_index_failed_and_rethrows()
    {
        var failing = new FakeEmbeddingGenerator(_ => throw new InvalidOperationException("Embedding service unavailable"));
        using var factory = WithEmbeddingGenerator(failing);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => Indexer(factory).RunAsync(CancellationToken.None));

        Assert.Equal("Embedding service unavailable", exception.Message);
        var state = factory.Services.GetRequiredService<IndexState>().Current;
        Assert.Equal((IndexStatus.Failed, "Embedding service unavailable"), (state.Status, state.Error));
    }

    [Fact]
    public async Task Second_run_while_a_run_is_active_returns_null_immediately()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var blocking = new FakeEmbeddingGenerator(text =>
        {
            entered.Set();
            release.Wait(TimeSpan.FromSeconds(10));
            return FakeEmbeddingGenerator.BagOfWordsVector(text);
        });
        using var factory = WithEmbeddingGenerator(blocking);
        var indexer = Indexer(factory);

        var firstRun = Task.Run(() => indexer.RunAsync(CancellationToken.None));
        Assert.True(entered.Wait(TimeSpan.FromSeconds(10)), "first run did not reach the embedding generator");

        var second = await indexer.RunAsync(CancellationToken.None);
        release.Set();

        Assert.Null(second);
        Assert.NotNull(await firstRun);
    }

    [Fact]
    public void Application_references_no_azure_package()
    {
        var references = typeof(KnowledgeIndexer).Assembly.GetReferencedAssemblies().Select(a => a.Name!).ToList();

        Assert.DoesNotContain(references, name =>
            name.StartsWith("Azure", StringComparison.Ordinal) ||
            name.StartsWith("OpenAI", StringComparison.Ordinal) ||
            name.StartsWith("Microsoft.SemanticKernel", StringComparison.Ordinal));
        Assert.Contains("Microsoft.Extensions.VectorData.Abstractions", references);
    }

    public void Dispose() => _factory.Dispose();

    private static KnowledgeIndexer Indexer(WebApplicationFactory<Program> factory) =>
        factory.Services.GetRequiredService<KnowledgeIndexer>();

    private WebApplicationFactory<Program> WithEmbeddingGenerator(FakeEmbeddingGenerator generator) =>
        _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(generator)));

    private static async Task<List<KnowledgeChunk>> StoredRecords(WebApplicationFactory<Program> factory)
    {
        var collection = factory.Services.GetRequiredService<VectorStoreCollection<string, KnowledgeChunk>>();
        return await collection.GetAsync(_ => true, top: 1000, new FilteredRecordRetrievalOptions<KnowledgeChunk> { IncludeVectors = true })
            .ToListAsync();
    }

    private static int CountChunksOfRealDocs()
    {
        using var factory = new CloudKnowledgeApiFactory().WithSetting("Knowledge:IndexOnStartup", "false");
        var chunker = factory.Services.GetRequiredService<MarkdownChunker>();
        return Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "docs"), "*.md")
            .Sum(file => chunker.Chunk(new MarkdownDocument(Path.GetFileName(file), File.ReadAllText(file))).Count);
    }
}
