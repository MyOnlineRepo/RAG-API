namespace CloudKnowledge.Application.Chunking;

/// <summary>A section-sized piece of a document — the unit that is embedded, searched and cited.</summary>
/// <param name="Id">"{document}#{sectionSlug}-{partIndex}", e.g. "troubleshooting.md#http-503-after-deployment-0".</param>
/// <param name="DocumentTitle">The H1, or the file name without extension if there is no H1.</param>
/// <param name="Section">The H2 text, or "Introduction" for the text before the first H2.</param>
/// <param name="Content">The raw section text without the heading.</param>
/// <param name="TextToEmbed">"{DocumentTitle} > {Section}\n\n{Content}" — the prefix is only used for embedding.</param>
public sealed record DocumentChunk(
    string Id,
    string DocumentName,
    string DocumentTitle,
    string Section,
    string Content,
    string TextToEmbed);
