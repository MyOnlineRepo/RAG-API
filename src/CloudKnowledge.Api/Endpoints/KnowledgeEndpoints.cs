using CloudKnowledge.Application.Indexing;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CloudKnowledge.Api.Endpoints;

public sealed record IndexResponse(int Documents, int Chunks, int EmbeddedNew, int FromCache, long DurationMs);

public static class KnowledgeEndpoints
{
    public static IEndpointRouteBuilder MapKnowledgeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/knowledge");

        group.MapPost("/index", IndexAsync);

        return app;
    }

    // No authentication: local demo only (SPEC §11).
    private static async Task<Results<Ok<IndexResponse>, ProblemHttpResult>> IndexAsync(
        KnowledgeIndexer indexer, IHostApplicationLifetime lifetime)
    {
        try
        {
            // Not tied to the request: a client that disconnects must not leave a half-built index behind.
            var result = await indexer.RunAsync(lifetime.ApplicationStopping);
            if (result is null)
            {
                return TypedResults.Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Indexing is already running",
                    detail: "Wait until the current run has finished and try again.");
            }

            return TypedResults.Ok(new IndexResponse(
                result.Documents, result.Chunks, result.EmbeddedNew, result.FromCache, (long)result.Duration.TotalMilliseconds));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Indexing failed",
                detail: exception.Message);
        }
    }
}
