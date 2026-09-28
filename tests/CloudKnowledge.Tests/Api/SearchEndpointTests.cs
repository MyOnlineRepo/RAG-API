using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CloudKnowledge.Application.Indexing;
using CloudKnowledge.Application.Search;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.VectorData;

namespace CloudKnowledge.Tests.Api;

public sealed class SearchEndpointTests : IDisposable
{
    // Indexing is triggered explicitly, so no test has to wait for the background run.
    private readonly CloudKnowledgeApiFactory _factory =
        new CloudKnowledgeApiFactory().WithSetting("Knowledge:IndexOnStartup", "false");

    [Fact]
    public async Task Search_returns_the_troubleshooting_section_first_with_descending_scores()
    {
        using var client = await IndexedClientAsync();

        var body = await client.GetFromJsonAsync<JsonElement>("/api/knowledge/search?q=HTTP 503 after deployment");

        var results = body.GetProperty("results").EnumerateArray().ToList();
        Assert.Equal("HTTP 503 after deployment", body.GetProperty("query").GetString());
        Assert.Equal(5, results.Count); // Rag:TopK default
        Assert.Equal("troubleshooting.md", results[0].GetProperty("document").GetString());
        Assert.Equal("HTTP 503 after deployment", results[0].GetProperty("section").GetString());
        Assert.False(string.IsNullOrWhiteSpace(results[0].GetProperty("content").GetString()));
        var scores = results.Select(r => r.GetProperty("score").GetDouble()).ToList();
        Assert.Equal(scores.OrderDescending(), scores);
    }

    [Fact]
    public async Task Top_limits_the_results_and_above_threshold_follows_min_score()
    {
        // Bag-of-words scores of whole sections are low (the best hit here is ~0.18), so the threshold is low too.
        _factory.WithSetting("Rag:MinScore", "0.15");
        using var client = await IndexedClientAsync();

        var body = await client.GetFromJsonAsync<JsonElement>("/api/knowledge/search?q=HTTP 503 after deployment&top=3");

        var results = body.GetProperty("results").EnumerateArray().ToList();
        Assert.Equal(0.15, body.GetProperty("minScore").GetDouble());
        Assert.Equal(3, results.Count);
        Assert.All(results, r => Assert.Equal(
            r.GetProperty("score").GetDouble() >= 0.15, r.GetProperty("aboveThreshold").GetBoolean()));
        Assert.Contains(results, r => r.GetProperty("aboveThreshold").GetBoolean());
        Assert.Contains(results, r => !r.GetProperty("aboveThreshold").GetBoolean());
    }

    [Theory]
    [InlineData("/api/knowledge/search", "q")]
    [InlineData("/api/knowledge/search?q=ab", "q")]
    [InlineData("/api/knowledge/search?q=deployment&top=0", "top")]
    [InlineData("/api/knowledge/search?q=deployment&top=21", "top")]
    public async Task Invalid_query_returns_400_problem_details(string url, string invalidParameter)
    {
        using var client = await IndexedClientAsync();

        using var response = await client.GetAsync(url);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.True(problem.GetProperty("errors").TryGetProperty(invalidParameter, out _));
    }

    [Fact]
    public async Task Too_long_query_returns_400()
    {
        using var client = await IndexedClientAsync();

        using var response = await client.GetAsync($"/api/knowledge/search?q={new string('a', 1001)}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Search_before_the_index_is_ready_returns_503_problem_details()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/api/knowledge/search?q=deployment");
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("NotStarted", problem.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Score_is_cosine_similarity_where_higher_means_more_similar()
    {
        using var client = await IndexedClientAsync();
        var collection = _factory.Services.GetRequiredService<VectorStoreCollection<string, KnowledgeChunk>>();
        var record = await collection.GetAsync("troubleshooting.md#http-503-after-deployment-0");

        // The stored text itself (with its embedding prefix) must match its own vector almost perfectly: a similarity
        // close to 1. A distance function would return a value close to 0 instead.
        var result = await _factory.Services.GetRequiredService<KnowledgeSearch>().SearchAsync(
            $"Troubleshooting > HTTP 503 after deployment\n\n{record!.Content}", 2, CancellationToken.None);

        Assert.Equal(record.Id.Split('#')[0], result.Results[0].Document);
        Assert.Equal(1.0, result.Results[0].Score, precision: 2);
        Assert.True(result.Results[1].Score < result.Results[0].Score);
    }

    [Fact]
    public async Task OpenApi_document_describes_search_and_index_in_development()
    {
        using var client = WithEnvironment("Development").CreateClient();

        var document = await client.GetFromJsonAsync<JsonElement>("/openapi/v1.json");

        var paths = document.GetProperty("paths");
        var search = paths.GetProperty("/api/knowledge/search").GetProperty("get");
        var index = paths.GetProperty("/api/knowledge/index").GetProperty("post");
        Assert.Equal("SearchKnowledge", search.GetProperty("operationId").GetString());
        Assert.Equal(["200", "400", "503"], search.GetProperty("responses").EnumerateObject().Select(p => p.Name).Order());
        Assert.Equal(["200", "409", "500"], index.GetProperty("responses").EnumerateObject().Select(p => p.Name).Order());
    }

    [Theory]
    [InlineData("Development", HttpStatusCode.OK)]
    [InlineData("Production", HttpStatusCode.NotFound)]
    public async Task Scalar_and_openapi_are_only_available_in_development(string environment, HttpStatusCode expected)
    {
        using var client = WithEnvironment(environment).CreateClient();

        using var scalar = await client.GetAsync("/scalar");
        using var openApi = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(expected, scalar.StatusCode);
        Assert.Equal(expected, openApi.StatusCode);
    }

    public void Dispose() => _factory.Dispose();

    private async Task<HttpClient> IndexedClientAsync()
    {
        var client = _factory.CreateClient();
        using var response = await client.PostAsync("/api/knowledge/index", null);
        response.EnsureSuccessStatusCode();
        return client;
    }

    private Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> WithEnvironment(string environment) =>
        _factory.WithWebHostBuilder(builder => builder.UseEnvironment(environment));
}
