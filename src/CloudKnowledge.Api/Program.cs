using CloudKnowledge.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCloudKnowledge(builder.Configuration);

var app = builder.Build();

app.Run();

public partial class Program;
