using CloudKnowledge.Application.Configuration;
using CloudKnowledge.Application.Indexing;
using CloudKnowledge.Application.Search;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace CloudKnowledge.Api.Endpoints;

public sealed record IndexResponse(int Documents, int Chunks, int EmbeddedNew, int FromCache, long DurationMs);

public static class KnowledgeEndpoints
{
    public const int MinQueryLength = 3;
    public const int MaxQueryLength = 1000;
    public const int MaxTop = 20;

    public static IEndpointRouteBuilder MapKnowledgeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/knowledge").WithTags("Knowledge");

        group.MapGet("/search", SearchAsync)
            .WithName("SearchKnowledge")
            .WithSummary("Search the documentation (retrieval only, no LLM)")
            .WithDescription(
                "Embeds the query and returns the most similar documentation sections with their cosine similarity. " +
                "Results are returned regardless of Rag:MinScore; aboveThreshold marks those that reach it.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        group.MapPost("/index", IndexAsync)
            .WithName("RebuildIndex")
            .WithSummary("Rebuild the index from docs/")
            .WithDescription(
                "Re-reads docs/, chunks and embeds it (unchanged chunks come from the embedding cache) and refills the " +
                "vector collection. No authentication: local demo only.")
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<Results<Ok<SearchResult>, ValidationProblem, ProblemHttpResult>> SearchAsync(
        string? q,
        int? top,
        KnowledgeSearch search,
        IndexState indexState,
        IOptions<RagOptions> ragOptions,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (q is null || q.Trim().Length is < MinQueryLength or > MaxQueryLength)
        {
            errors["q"] = [$"q is required and must be {MinQueryLength}–{MaxQueryLength} characters long."];
        }

        if (top is < 1 or > MaxTop)
        {
            errors["top"] = [$"top must be between 1 and {MaxTop}."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        if (IndexNotReady(indexState) is { } notReady)
        {
            return notReady;
        }

        return TypedResults.Ok(await search.SearchAsync(q!.Trim(), top ?? ragOptions.Value.TopK, cancellationToken));
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

    private static ProblemHttpResult? IndexNotReady(IndexState indexState)
    {
        var current = indexState.Current;
        return current.Status == IndexStatus.Ready
            ? null
            : TypedResults.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "The index is not ready",
                detail: $"Index status: {current.Status}. See /health/ready.");
    }
}
