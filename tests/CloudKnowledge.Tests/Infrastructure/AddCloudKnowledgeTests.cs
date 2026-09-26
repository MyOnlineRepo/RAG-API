using CloudKnowledge.Tests.Fakes;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CloudKnowledge.Tests.Infrastructure;

public sealed class AddCloudKnowledgeTests
{
    [Fact]
    public void Resolves_ai_clients_with_valid_configuration()
    {
        using var factory = new CloudKnowledgeApiFactory();

        Assert.NotNull(factory.Services.GetRequiredService<IChatClient>());
        Assert.NotNull(factory.Services.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>());
    }

    [Theory]
    [InlineData("AzureOpenAI:Endpoint", "", "AzureOpenAI:Endpoint is required")]
    [InlineData("AzureOpenAI:Endpoint", "http://insecure.openai.azure.com/", "must be an absolute https URI")]
    [InlineData("AzureOpenAI:Endpoint", "not a uri", "must be an absolute https URI")]
    [InlineData("AzureOpenAI:ChatDeployment", "", "AzureOpenAI:ChatDeployment is required")]
    [InlineData("Rag:TopK", "0", "Rag:TopK must be between 1 and 20")]
    [InlineData("Rag:MinScore", "1.5", "Rag:MinScore must be between 0.0 and 1.0")]
    [InlineData("Chunking:MaxTokens", "10", "Chunking:MaxTokens must be between 50 and 2000")]
    public void Startup_fails_with_clear_message_for_invalid_configuration(string key, string value, string expectedMessage)
    {
        using var factory = new CloudKnowledgeApiFactory().WithSetting(key, value);

        var exception = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());

        Assert.Contains(expectedMessage, exception.Message);
    }

    [Fact]
    public void Tests_can_replace_ai_clients_with_fakes()
    {
        var chat = new FakeChatClient();
        var embeddings = new FakeEmbeddingGenerator(_ => [1f, 0f]);
        using var factory = new CloudKnowledgeApiFactory().WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IChatClient>(chat);
                services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(embeddings);
            }));

        Assert.Same(chat, factory.Services.GetRequiredService<IChatClient>());
        Assert.Same(embeddings, factory.Services.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>());
    }
}
