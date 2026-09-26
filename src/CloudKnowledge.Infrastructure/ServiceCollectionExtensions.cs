using Azure.AI.OpenAI;
using Azure.Identity;
using CloudKnowledge.Application.Configuration;
using CloudKnowledge.Application.Indexing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace CloudKnowledge.Infrastructure;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers validated options, the shared index state and the Azure OpenAI backed AI clients.
    /// Authentication uses <see cref="DefaultAzureCredential"/> (Entra ID); no API keys.
    /// Nothing here calls Azure at startup — the first request happens when a client is used.
    /// </summary>
    public static IServiceCollection AddCloudKnowledge(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddValidatedOptions<AzureOpenAIOptions>(configuration, AzureOpenAIOptions.SectionName);
        services.AddValidatedOptions<RagOptions>(configuration, RagOptions.SectionName);
        services.AddValidatedOptions<ChunkingOptions>(configuration, ChunkingOptions.SectionName);
        services.AddValidatedOptions<KnowledgeOptions>(configuration, KnowledgeOptions.SectionName);

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IndexState>();

        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<AzureOpenAIOptions>>().Value;
            return new AzureOpenAIClient(new Uri(options.Endpoint), new DefaultAzureCredential());
        });

        services.AddChatClient(sp =>
        {
            var options = sp.GetRequiredService<IOptions<AzureOpenAIOptions>>().Value;
            return sp.GetRequiredService<AzureOpenAIClient>()
                .GetChatClient(options.ChatDeployment)
                .AsIChatClient();
        });

        services.AddEmbeddingGenerator(sp =>
        {
            var options = sp.GetRequiredService<IOptions<AzureOpenAIOptions>>().Value;
            return sp.GetRequiredService<AzureOpenAIClient>()
                .GetEmbeddingClient(options.EmbeddingDeployment)
                .AsIEmbeddingGenerator();
        });

        return services;
    }

    private static void AddValidatedOptions<TOptions>(
        this IServiceCollection services, IConfiguration configuration, string sectionName)
        where TOptions : class
    {
        services.AddOptions<TOptions>()
            .Bind(configuration.GetSection(sectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
    }
}
