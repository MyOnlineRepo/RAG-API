using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace CloudKnowledge.Tests;

/// <summary>
/// Test host with valid, fake configuration. Never talks to Azure: the endpoint is syntactically valid
/// and the AI clients are created lazily, so no request leaves the process unless a test uses them.
/// </summary>
public class CloudKnowledgeApiFactory : WebApplicationFactory<Program>
{
    public static readonly IReadOnlyDictionary<string, string?> ValidSettings = new Dictionary<string, string?>
    {
        ["AzureOpenAI:Endpoint"] = "https://unit-test.openai.azure.com/",
        ["AzureOpenAI:ChatDeployment"] = "test-chat",
        ["AzureOpenAI:EmbeddingDeployment"] = "test-embedding",
    };

    private readonly Dictionary<string, string?> _settings = new(ValidSettings);

    public CloudKnowledgeApiFactory WithSetting(string key, string? value)
    {
        _settings[key] = value;
        return this;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(_settings));
    }
}
