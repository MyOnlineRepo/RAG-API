using CloudKnowledge.Application.Chunking;
using CloudKnowledge.Application.Configuration;
using Microsoft.Extensions.Options;

namespace CloudKnowledge.Tests.KnowledgeBase;

/// <summary>The chunker against the real knowledge base in <c>docs/</c> (copied into the test output).</summary>
public sealed class KnowledgeBaseChunkingTests
{
    private static readonly int MaxTokens = new ChunkingOptions().MaxTokens;

    private static readonly IReadOnlyList<DocumentChunk> Chunks = ChunkAll();

    private static IReadOnlyList<DocumentChunk> ChunkAll()
    {
        var chunker = new MarkdownChunker(Options.Create(new ChunkingOptions()));
        return Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "docs"), "*.md")
            .Order()
            .SelectMany(file => chunker.Chunk(new MarkdownDocument(Path.GetFileName(file), File.ReadAllText(file))))
            .ToList();
    }

    [Fact]
    public void Every_document_yields_at_least_three_chunks()
    {
        var perDocument = Chunks.GroupBy(c => c.DocumentName).ToDictionary(g => g.Key, g => g.Count());

        Assert.Equal(9, perDocument.Count);
        Assert.All(perDocument, pair => Assert.True(pair.Value >= 3, $"{pair.Key} has only {pair.Value} chunks"));
    }

    [Fact]
    public void Ids_are_unique_across_the_knowledge_base()
    {
        Assert.Equal(Chunks.Count, Chunks.Select(c => c.Id).Distinct().Count());
    }

    [Fact]
    public void Troubleshooting_has_the_http_503_section()
    {
        Assert.Contains(Chunks, c => c.DocumentName == "troubleshooting.md" && c.Section == "HTTP 503 after deployment");
    }

    [Fact]
    public void No_chunk_exceeds_max_tokens_unless_it_is_a_single_block()
    {
        var oversized = Chunks.Where(c => MarkdownChunker.EstimateTokens(c.Content) > MaxTokens);

        Assert.All(oversized, c => Assert.DoesNotContain("\n\n", c.Content));
    }

}
