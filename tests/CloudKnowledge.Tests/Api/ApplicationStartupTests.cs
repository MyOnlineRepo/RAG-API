using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CloudKnowledge.Tests.Api;

public sealed class ApplicationStartupTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Application_starts_and_answers_requests()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
