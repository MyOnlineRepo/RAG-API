using CloudKnowledge.Tests.Fakes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CloudKnowledge.Tests;

/// <summary>
/// Test host with valid, fake configuration and fake AI clients. Never talks to Azure: the chat client and the
/// embedding generator are replaced by <see cref="ChatClient"/> and <see cref="EmbeddingGenerator"/>.
/// </summary>
public class CloudKnowledgeApiFactory : WebApplicationFactory<Program>
{
    public static readonly IReadOnlyDictionary<string, string?> ValidSettings = new Dictionary<string, string?>
    {
        ["AzureOpenAI:Endpoint"] = "https://unit-test.openai.azure.com/",
        ["AzureOpenAI:ChatDeployment"] = "test-chat",
        ["AzureOpenAI:EmbeddingDeployment"] = "test-embedding",
    };

    private readonly Dictionary<string, string?> _settings = new(ValidSettings)
    {
        // Every test host gets its own cache file, so tests never see embeddings from earlier runs.
        ["Knowledge:EmbeddingCachePath"] = Path.Combine(Path.GetTempPath(), "cloudknowledge-tests", $"{Guid.NewGuid()}.json"),
    };

    public FakeChatClient ChatClient { get; } = new();

    public FakeEmbeddingGenerator EmbeddingGenerator { get; } = FakeEmbeddingGenerator.BagOfWords();

    /// <summary><c>false</c> keeps the real Azure OpenAI clients (see <see cref="RealAiClientsApiFactory"/>).</summary>
    protected virtual bool UseFakeAiClients => true;

    public CloudKnowledgeApiFactory WithSetting(string key, string? value)
    {
        _settings[key] = value;
        return this;
    }

    public string EmbeddingCachePath => _settings["Knowledge:EmbeddingCachePath"]!;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(_settings));

        if (UseFakeAiClients)
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IChatClient>(ChatClient);
                services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(EmbeddingGenerator);
            });
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (File.Exists(EmbeddingCachePath))
        {
            File.Delete(EmbeddingCachePath);
        }
    }
}

/// <summary>
/// Keeps the real, lazily created Azure OpenAI clients. Only for DI-registration tests, which never call the clients.
/// </summary>
public sealed class RealAiClientsApiFactory : CloudKnowledgeApiFactory
{
    protected override bool UseFakeAiClients => false;
}
