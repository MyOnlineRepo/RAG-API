namespace CloudKnowledge.Application.Chunking;

/// <summary>One Markdown file of the knowledge base. <see cref="Name"/> is the file name, e.g. "troubleshooting.md".</summary>
public sealed record MarkdownDocument(string Name, string Content);
