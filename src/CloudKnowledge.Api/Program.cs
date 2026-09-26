using CloudKnowledge.Api.Health;
using CloudKnowledge.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCloudKnowledge(builder.Configuration);
builder.Services.AddCloudKnowledgeHealthChecks();

var app = builder.Build();

app.MapCloudKnowledgeHealthChecks();

app.Run();

public partial class Program;
