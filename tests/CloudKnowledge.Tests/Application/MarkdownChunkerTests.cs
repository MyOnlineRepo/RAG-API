using CloudKnowledge.Application.Chunking;
using CloudKnowledge.Application.Configuration;
using Microsoft.Extensions.Options;

namespace CloudKnowledge.Tests.Application;

public sealed class MarkdownChunkerTests
{
    private static MarkdownChunker Chunker(int maxTokens = 400) =>
        new(Options.Create(new ChunkingOptions { MaxTokens = maxTokens }));

    private static IReadOnlyList<DocumentChunk> Chunk(string markdown, int maxTokens = 400, string name = "doc.md") =>
        Chunker(maxTokens).Chunk(new MarkdownDocument(name, markdown));

    // 50 tokens ≈ 200 characters; each paragraph below is ~120 characters, so two do not fit into one part.
    private static string Paragraph(string word) => string.Join(' ', Enumerable.Repeat(word, 20));

    [Fact]
    public void Splits_at_h2_headings()
    {
        var chunks = Chunk("# Title\n\n## First\n\nAlpha.\n\n## Second\n\nBeta.\n");

        Assert.Equal(["First", "Second"], chunks.Select(c => c.Section));
        Assert.Equal(["Alpha.", "Beta."], chunks.Select(c => c.Content));
        Assert.All(chunks, c => Assert.Equal("Title", c.DocumentTitle));
    }

    [Fact]
    public void Text_before_the_first_h2_becomes_the_introduction()
    {
        var chunks = Chunk("# Title\n\nIntro text.\n\n## First\n\nAlpha.");

        Assert.Equal(("Introduction", "Intro text."), (chunks[0].Section, chunks[0].Content));
        Assert.Equal("doc.md#introduction-0", chunks[0].Id);
    }

    [Fact]
    public void Empty_introduction_is_skipped()
    {
        var chunks = Chunk("# Title\n\n\n## First\n\nAlpha.");

        Assert.Equal(["First"], chunks.Select(c => c.Section));
    }

    [Fact]
    public void Title_falls_back_to_the_file_name_without_h1()
    {
        var chunks = Chunk("## First\n\nAlpha.", name: "storage.md");

        Assert.Equal("storage", chunks.Single().DocumentTitle);
    }

    [Fact]
    public void Heading_inside_a_code_block_is_not_a_section()
    {
        var chunks = Chunk("# Title\n\n## Real\n\n```markdown\n## Not a heading\n# Not a title\n```\n");

        var chunk = Assert.Single(chunks);
        Assert.Equal("Real", chunk.Section);
        Assert.Contains("## Not a heading", chunk.Content);
    }

    [Fact]
    public void Long_section_is_split_at_blank_lines_without_overlap()
    {
        string[] paragraphs = [Paragraph("alpha"), Paragraph("beta"), Paragraph("gamma")];

        var chunks = Chunk($"## Long\n\n{string.Join("\n\n", paragraphs)}", maxTokens: 50);

        Assert.Equal(paragraphs, chunks.Select(c => c.Content));
        Assert.Equal(["doc.md#long-0", "doc.md#long-1", "doc.md#long-2"], chunks.Select(c => c.Id));
        Assert.All(chunks, c => Assert.Equal("Long", c.Section));
    }

    [Fact]
    public void Small_paragraphs_are_packed_into_one_part()
    {
        var chunks = Chunk($"## Mixed\n\nOne.\n\nTwo.\n\n{Paragraph("alpha")}\n\n{Paragraph("beta")}", maxTokens: 50);

        Assert.Equal($"One.\n\nTwo.\n\n{Paragraph("alpha")}", chunks[0].Content);
        Assert.Equal(Paragraph("beta"), chunks[1].Content);
    }

    [Fact]
    public void Code_block_with_blank_lines_is_never_split()
    {
        var code = $"```bash\n{Paragraph("echo")}\n\n{Paragraph("ls")}\n```";

        var chunks = Chunk($"## Code\n\n{Paragraph("intro")}\n\n{code}\n\n{Paragraph("outro")}", maxTokens: 50);

        Assert.Contains(chunks, c => c.Content == code);
        Assert.All(chunks, c => Assert.Equal(1, c.Content.Split("```").Length % 2)); // fences come in pairs
    }

    [Fact]
    public void List_with_blank_lines_between_items_is_never_split()
    {
        var list = $"- {Paragraph("one")}\n\n- {Paragraph("two")}\n\n  continued\n\n1. {Paragraph("three")}";

        var chunks = Chunk($"## List\n\n{Paragraph("intro")}\n\n{list}\n\n{Paragraph("outro")}", maxTokens: 50);

        Assert.Equal([Paragraph("intro"), list, Paragraph("outro")], chunks.Select(c => c.Content));
    }

    [Fact]
    public void Oversized_single_block_stays_whole()
    {
        var huge = string.Join(' ', Enumerable.Repeat("word", 200));

        var chunk = Assert.Single(Chunk($"## Huge\n\n{huge}", maxTokens: 50));

        Assert.Equal(huge, chunk.Content);
        Assert.True(MarkdownChunker.EstimateTokens(chunk.Content) > 50);
    }

    [Fact]
    public void Text_to_embed_prefixes_title_and_section()
    {
        var chunk = Chunk("# Troubleshooting\n\n## HTTP 503 after deployment\n\nCheck the revision.").Single();

        Assert.Equal("Troubleshooting > HTTP 503 after deployment\n\nCheck the revision.", chunk.TextToEmbed);
        Assert.Equal("Check the revision.", chunk.Content);
    }

    [Fact]
    public void Ids_are_stable_and_unique_even_for_repeated_headings()
    {
        const string markdown = "# T\n\n## Notes\n\nA.\n\n## Notes\n\nB.";

        var first = Chunk(markdown).Select(c => c.Id).ToArray();
        var second = Chunk(markdown).Select(c => c.Id).ToArray();

        Assert.Equal(["doc.md#notes-0", "doc.md#notes-2-0"], first);
        Assert.Equal(first, second);
    }

    [Theory]
    [InlineData("HTTP 503 after deployment", "http-503-after-deployment")]
    [InlineData("  Blob Storage & Managed Identity!  ", "blob-storage-managed-identity")]
    [InlineData("CI/CD -- pipeline", "ci-cd-pipeline")]
    public void Slug_is_lower_case_with_collapsed_dashes(string heading, string expected)
    {
        Assert.Equal(expected, MarkdownChunker.Slugify(heading));
    }

    [Fact]
    public void Windows_line_endings_are_handled()
    {
        var chunks = Chunk("# Title\r\n\r\n## First\r\n\r\nAlpha.\r\n");

        Assert.Equal(("First", "Alpha."), (chunks.Single().Section, chunks.Single().Content));
    }
}
