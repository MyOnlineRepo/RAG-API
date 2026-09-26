using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CloudKnowledge.Api.Health;

public static class HealthEndpoints
{
    private const string ReadyTag = "ready";

    public static IServiceCollection AddCloudKnowledgeHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<IndexReadinessHealthCheck>("index", tags: [ReadyTag]);
        return services;
    }

    public static WebApplication MapCloudKnowledgeHealthChecks(this WebApplication app)
    {
        // Liveness runs no checks: the process answering is all it reports.
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = WriteResponse,
        });

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(ReadyTag),
            ResponseWriter = WriteResponse,
        });

        return app;
    }

    private static Task WriteResponse(HttpContext context, HealthReport report)
    {
        var body = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                description = entry.Value.Description,
            }),
        };

        context.Response.ContentType = "application/json";
        return context.Response.WriteAsync(JsonSerializer.Serialize(body, JsonSerializerOptions.Web));
    }
}
