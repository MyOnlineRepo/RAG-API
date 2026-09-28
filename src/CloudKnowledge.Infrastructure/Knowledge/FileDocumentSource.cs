using CloudKnowledge.Application.Chunking;
using CloudKnowledge.Application.Configuration;
using CloudKnowledge.Application.Indexing;
using Microsoft.Extensions.Options;

namespace CloudKnowledge.Infrastructure.Knowledge;

/// <summary>Reads all <c>*.md</c> files from <see cref="KnowledgeOptions.DocsPath"/>, ordered by file name.</summary>
public sealed class FileDocumentSource(IOptions<KnowledgeOptions> options) : IDocumentSource
{
    private readonly string _docsPath = KnowledgePaths.Resolve(options.Value.DocsPath);

    public async Task<IReadOnlyList<MarkdownDocument>> LoadAsync(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(_docsPath))
        {
            throw new DirectoryNotFoundException(
                $"Knowledge base folder '{_docsPath}' does not exist. Check Knowledge:DocsPath.");
        }

        var documents = new List<MarkdownDocument>();
        foreach (var file in Directory.GetFiles(_docsPath, "*.md").Order(StringComparer.Ordinal))
        {
            documents.Add(new MarkdownDocument(Path.GetFileName(file), await File.ReadAllTextAsync(file, cancellationToken)));
        }

        return documents;
    }
}
