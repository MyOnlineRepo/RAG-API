using CloudKnowledge.Application.Chunking;

namespace CloudKnowledge.Application.Indexing;

/// <summary>Provides the Markdown documents of the knowledge base.</summary>
public interface IDocumentSource
{
    Task<IReadOnlyList<MarkdownDocument>> LoadAsync(CancellationToken cancellationToken);
}
