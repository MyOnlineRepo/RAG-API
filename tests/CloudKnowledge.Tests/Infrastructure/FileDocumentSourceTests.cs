using CloudKnowledge.Application.Configuration;
using CloudKnowledge.Infrastructure.Knowledge;
using Microsoft.Extensions.Options;

namespace CloudKnowledge.Tests.Infrastructure;

public sealed class FileDocumentSourceTests
{
    [Fact]
    public async Task Loads_the_nine_documents_in_name_order()
    {
        var source = new FileDocumentSource(Options.Create(new KnowledgeOptions()));

        var documents = await source.LoadAsync(CancellationToken.None);

        Assert.Equal<string>(
            ["api.md", "architecture.md", "authentication.md", "container-apps.md", "deployment.md",
             "monitoring.md", "security.md", "storage.md", "troubleshooting.md"],
            documents.Select(d => d.Name));
        Assert.StartsWith("# ", documents[0].Content);
    }

    [Fact]
    public async Task Missing_folder_throws_with_the_resolved_path()
    {
        var source = new FileDocumentSource(Options.Create(new KnowledgeOptions { DocsPath = "no-such-folder" }));

        var exception = await Assert.ThrowsAsync<DirectoryNotFoundException>(() => source.LoadAsync(CancellationToken.None));

        Assert.Contains(Path.Combine(AppContext.BaseDirectory, "no-such-folder"), exception.Message);
        Assert.Contains("Knowledge:DocsPath", exception.Message);
    }
}
