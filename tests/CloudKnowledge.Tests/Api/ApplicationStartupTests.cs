using System.Net;

namespace CloudKnowledge.Tests.Api;

public sealed class ApplicationStartupTests(CloudKnowledgeApiFactory factory) : IClassFixture<CloudKnowledgeApiFactory>
{
    [Fact]
    public async Task Application_starts_and_answers_requests()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
