using CloudKnowledge.Api.Endpoints;
using CloudKnowledge.Api.Health;
using CloudKnowledge.Api.Indexing;
using CloudKnowledge.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCloudKnowledge(builder.Configuration);
builder.Services.AddCloudKnowledgeHealthChecks();
builder.Services.AddHostedService<KnowledgeIndexingService>();
builder.Services.AddProblemDetails();

var app = builder.Build();

app.MapCloudKnowledgeHealthChecks();
app.MapKnowledgeEndpoints();

app.Run();

public partial class Program;
