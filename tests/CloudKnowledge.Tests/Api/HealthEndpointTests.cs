using System.Net;
using System.Text.Json;
using CloudKnowledge.Application.Indexing;
using Microsoft.Extensions.DependencyInjection;

namespace CloudKnowledge.Tests.Api;

public sealed class HealthEndpointTests : IDisposable
{
    // A fresh host per test: IndexState is a singleton, so tests must not share it.
    private readonly CloudKnowledgeApiFactory _factory = new();

    private IndexState IndexState => _factory.Services.GetRequiredService<IndexState>();

    [Fact]
    public async Task Live_is_healthy_even_without_an_index()
    {
        var (status, body) = await GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("Healthy", body.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Ready_is_unavailable_before_indexing()
    {
        var (status, body) = await GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, status);
        Assert.Contains("NotStarted", IndexDescription(body));
    }

    [Fact]
    public async Task Ready_is_healthy_once_the_index_is_ready()
    {
        IndexState.MarkReady(10);

        var (status, body) = await GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains("10 chunks", IndexDescription(body));
    }

    [Fact]
    public async Task Ready_reports_the_error_when_indexing_failed()
    {
        IndexState.MarkFailed("boom");

        var (status, body) = await GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, status);
        Assert.Contains("boom", IndexDescription(body));
    }

    public void Dispose() => _factory.Dispose();

    private async Task<(HttpStatusCode Status, JsonElement Body)> GetAsync(string path)
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync(path);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        return (response.StatusCode, body);
    }

    private static string IndexDescription(JsonElement body) =>
        body.GetProperty("checks").EnumerateArray()
            .Single(check => check.GetProperty("name").GetString() == "index")
            .GetProperty("description").GetString()!;
}
