using CloudKnowledge.Application.Configuration;
using CloudKnowledge.Infrastructure.Knowledge;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CloudKnowledge.Tests.Infrastructure;

public sealed class KnowledgePathsTests
{
    [Fact]
    public void Relative_path_resolves_below_the_base_directory()
    {
        var resolved = KnowledgePaths.Resolve(".cache/embeddings.json");

        Assert.Equal(Path.Combine(AppContext.BaseDirectory, ".cache", "embeddings.json"), resolved);
    }

    [Fact]
    public void Absolute_path_is_returned_unchanged()
    {
        var absolute = Path.Combine(Path.GetTempPath(), "knowledge", "docs");

        Assert.Equal(absolute, KnowledgePaths.Resolve(absolute));
    }

    [Fact]
    public void Resolved_docs_path_of_the_test_host_contains_the_nine_documents()
    {
        using var factory = new CloudKnowledgeApiFactory();
        var options = factory.Services.GetRequiredService<IOptions<KnowledgeOptions>>().Value;

        var files = Directory.GetFiles(KnowledgePaths.Resolve(options.DocsPath))
            .Select(file => Path.GetFileName(file))
            .Order()
            .ToArray();

        Assert.Equal<string>(
            ["api.md", "architecture.md", "authentication.md", "container-apps.md", "deployment.md",
             "monitoring.md", "security.md", "storage.md", "troubleshooting.md"],
            files);
    }
}
