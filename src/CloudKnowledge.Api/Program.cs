using CloudKnowledge.Api.Endpoints;
using CloudKnowledge.Api.Health;
using CloudKnowledge.Api.Indexing;
using CloudKnowledge.Infrastructure;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCloudKnowledge(builder.Configuration);
builder.Services.AddCloudKnowledgeHealthChecks();
builder.Services.AddHostedService<KnowledgeIndexingService>();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();                // /openapi/v1.json
    app.MapScalarApiReference();     // /scalar
}

app.MapCloudKnowledgeHealthChecks();
app.MapKnowledgeEndpoints();

app.Run();

public partial class Program;
