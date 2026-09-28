using CloudKnowledge.Application.Configuration;
using CloudKnowledge.Application.Indexing;
using Microsoft.Extensions.Options;

namespace CloudKnowledge.Api.Indexing;

/// <summary>
/// Builds the index once when the API starts (if <see cref="KnowledgeOptions.IndexOnStartup"/> is set).
/// A failure is logged and visible in <c>/health/ready</c>; the API itself keeps running.
/// </summary>
public sealed class KnowledgeIndexingService(
    KnowledgeIndexer indexer,
    IOptions<KnowledgeOptions> options,
    ILogger<KnowledgeIndexingService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.IndexOnStartup)
        {
            logger.LogInformation("Indexing on startup is disabled (Knowledge:IndexOnStartup = false)");
            return;
        }

        try
        {
            if (await indexer.RunAsync(stoppingToken) is null)
            {
                logger.LogInformation("Indexing on startup skipped: another indexing run is already active");
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The API is shutting down.
        }
        catch (Exception exception)
        {
            // The indexer has already logged the error and set the state to Failed.
            logger.LogWarning("Indexing on startup failed ({Error}); the API keeps running and /health/ready reports the error",
                exception.Message);
        }
    }
}
