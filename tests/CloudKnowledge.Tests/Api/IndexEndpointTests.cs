using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CloudKnowledge.Application.Indexing;
using CloudKnowledge.Tests.Fakes;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CloudKnowledge.Tests.Api;

public sealed class IndexEndpointTests : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly CloudKnowledgeApiFactory _factory = new();

    [Fact]
    public async Task Index_is_built_on_startup_and_ready_reports_the_chunk_count()
    {
        using var client = _factory.CreateClient();

        var body = await WaitForReadyAsync(client, HttpStatusCode.OK);

        var chunks = _factory.Services.GetRequiredService<IndexState>().Current.ChunkCount;
        Assert.True(chunks > 0);
        Assert.Contains($"{chunks} chunks", IndexDescription(body));
    }

    [Fact]
    public async Task Post_index_builds_the_index_when_startup_indexing_is_disabled()
    {
        using var client = _factory.WithSetting("Knowledge:IndexOnStartup", "false").CreateClient();
        var (readyBefore, before) = await GetJsonAsync(client, "/health/ready");

        using var response = await client.PostAsync("/api/knowledge/index", null);
        var stats = await ReadJsonAsync(response);
        var (readyAfter, _) = await GetJsonAsync(client, "/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, readyBefore);
        Assert.Contains("NotStarted", IndexDescription(before));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(9, stats.GetProperty("documents").GetInt32());
        Assert.True(stats.GetProperty("chunks").GetInt32() > 0);
        Assert.True(stats.GetProperty("durationMs").GetInt64() >= 0);
        Assert.Equal(HttpStatusCode.OK, readyAfter);
    }

    [Fact]
    public async Task Second_post_index_takes_all_embeddings_from_the_cache()
    {
        using var client = _factory.WithSetting("Knowledge:IndexOnStartup", "false").CreateClient();
        using var first = await client.PostAsync("/api/knowledge/index", null);

        using var second = await client.PostAsync("/api/knowledge/index", null);
        var stats = await ReadJsonAsync(second);

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(0, stats.GetProperty("embeddedNew").GetInt32());
        Assert.Equal(stats.GetProperty("chunks").GetInt32(), stats.GetProperty("fromCache").GetInt32());
    }

    [Fact]
    public async Task Post_index_while_a_run_is_active_returns_409()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var blocking = new FakeEmbeddingGenerator(text =>
        {
            entered.Set();
            release.Wait(Timeout);
            return FakeEmbeddingGenerator.BagOfWordsVector(text);
        });
        _factory.WithSetting("Knowledge:IndexOnStartup", "false");
        using var factory = WithEmbeddingGenerator(blocking);
        using var client = factory.CreateClient();

        var firstRun = Task.Run(() => client.PostAsync("/api/knowledge/index", null));
        Assert.True(entered.Wait(Timeout), "first run did not reach the embedding generator");
        using var second = await client.PostAsync("/api/knowledge/index", null);
        release.Set();
        using var first = await firstRun;

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("application/problem+json", second.Content.Headers.ContentType?.MediaType);
        Assert.Equal(409, (await ReadJsonAsync(second)).GetProperty("status").GetInt32());
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
    }

    [Fact]
    public async Task Post_index_returns_500_problem_details_when_indexing_fails()
    {
        _factory.WithSetting("Knowledge:IndexOnStartup", "false");
        using var factory = WithEmbeddingGenerator(
            new FakeEmbeddingGenerator(_ => throw new InvalidOperationException("Embedding service unavailable")));
        using var client = factory.CreateClient();

        using var response = await client.PostAsync("/api/knowledge/index", null);
        var problem = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Embedding service unavailable", problem.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Failing_indexing_on_startup_keeps_the_api_alive_and_ready_reports_the_error()
    {
        using var factory = WithEmbeddingGenerator(
            new FakeEmbeddingGenerator(_ => throw new InvalidOperationException("Embedding service unavailable")));
        using var client = factory.CreateClient();

        var ready = await WaitForReadyAsync(client, HttpStatusCode.ServiceUnavailable, "Indexing failed");
        var (live, _) = await GetJsonAsync(client, "/health/live");

        Assert.Equal(HttpStatusCode.OK, live);
        Assert.Contains("Embedding service unavailable", IndexDescription(ready));
    }

    public void Dispose() => _factory.Dispose();

    private WebApplicationFactory<Program> WithEmbeddingGenerator(FakeEmbeddingGenerator generator) =>
        _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(generator)));

    // Indexing on startup runs in the background, so readiness is polled.
    private static async Task<JsonElement> WaitForReadyAsync(HttpClient client, HttpStatusCode expected, string? descriptionContains = null)
    {
        var deadline = DateTime.UtcNow + Timeout;
        while (true)
        {
            var (status, body) = await GetJsonAsync(client, "/health/ready");
            var description = IndexDescription(body);
            if (status == expected && (descriptionContains is null || description.Contains(descriptionContains)))
            {
                return body;
            }

            Assert.True(DateTime.UtcNow < deadline, $"/health/ready did not reach {expected}; last: {status} '{description}'");
            await Task.Delay(50);
        }
    }

    private static async Task<(HttpStatusCode Status, JsonElement Body)> GetJsonAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        return (response.StatusCode, await ReadJsonAsync(response));
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>());

    private static string IndexDescription(JsonElement body) =>
        body.GetProperty("checks").EnumerateArray()
            .Single(check => check.GetProperty("name").GetString() == "index")
            .GetProperty("description").GetString()!;
}
