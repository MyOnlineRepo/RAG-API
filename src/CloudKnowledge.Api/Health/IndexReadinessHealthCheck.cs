using CloudKnowledge.Application.Indexing;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CloudKnowledge.Api.Health;

/// <summary>Ready only once the knowledge index has been built.</summary>
public sealed class IndexReadinessHealthCheck(IndexState indexState) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var index = indexState.Current;
        var result = index.Status switch
        {
            IndexStatus.Ready => HealthCheckResult.Healthy($"Index ready with {index.ChunkCount} chunks."),
            IndexStatus.Failed => HealthCheckResult.Unhealthy($"Indexing failed: {index.Error}"),
            _ => HealthCheckResult.Unhealthy($"Index status: {index.Status}."),
        };

        return Task.FromResult(result);
    }
}
