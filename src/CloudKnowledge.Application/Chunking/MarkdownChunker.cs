using System.Text.RegularExpressions;
using CloudKnowledge.Application.Configuration;
using Microsoft.Extensions.Options;

namespace CloudKnowledge.Application.Chunking;

/// <summary>
/// Splits a Markdown document into chunks (SPEC §8): one per H2 section, long sections split at blank lines
/// into parts of at most <see cref="ChunkingOptions.MaxTokens"/>. Code blocks and lists are never split. No overlap.
/// Pure logic, no I/O.
/// </summary>
public sealed partial class MarkdownChunker(IOptions<ChunkingOptions> options)
{
    public const string IntroductionSection = "Introduction";

    private readonly int _maxTokens = options.Value.MaxTokens;

    /// <summary>Token estimate without a tokenizer: roughly four characters per token.</summary>
    public static int EstimateTokens(string text) => (text.Length + 3) / 4;

    public IReadOnlyList<DocumentChunk> Chunk(MarkdownDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var (title, sections) = ParseSections(document);
        var chunks = new List<DocumentChunk>();
        var usedSlugs = new Dictionary<string, int>();

        foreach (var (heading, content) in sections)
        {
            if (content.Length == 0)
            {
                continue;
            }

            var slug = UniqueSlug(Slugify(heading), usedSlugs);
            var parts = EstimateTokens(content) <= _maxTokens ? [content] : SplitIntoParts(content);

            for (var partIndex = 0; partIndex < parts.Count; partIndex++)
            {
                chunks.Add(new DocumentChunk(
                    Id: $"{document.Name}#{slug}-{partIndex}",
                    DocumentName: document.Name,
                    DocumentTitle: title,
                    Section: heading,
                    Content: parts[partIndex],
                    TextToEmbed: $"{title} > {heading}\n\n{parts[partIndex]}"));
            }
        }

        return chunks;
    }

    /// <summary>Lower-case; every run of non-alphanumeric characters becomes one "-"; no leading or trailing "-".</summary>
    public static string Slugify(string heading) =>
        NonAlphanumeric().Replace(heading.ToLowerInvariant(), "-").Trim('-');

    // Title from the H1 (or the file name), sections at "## " headings outside fenced code blocks.
    private static (string Title, List<(string Heading, string Content)> Sections) ParseSections(MarkdownDocument document)
    {
        string? title = null;
        var sections = new List<(string Heading, string Content)>();
        var heading = IntroductionSection;
        var lines = new List<string>();
        var inFence = false;

        foreach (var line in document.Content.ReplaceLineEndings("\n").Split('\n'))
        {
            if (IsFence(line))
            {
                inFence = !inFence;
            }
            else if (!inFence && line.StartsWith("## ", StringComparison.Ordinal))
            {
                sections.Add((heading, string.Join('\n', lines).Trim()));
                heading = line[3..].Trim();
                lines.Clear();
                continue;
            }
            else if (!inFence && title is null && sections.Count == 0 && line.StartsWith("# ", StringComparison.Ordinal))
            {
                title = line[2..].Trim();
                continue;
            }

            lines.Add(line);
        }

        sections.Add((heading, string.Join('\n', lines).Trim()));
        return (title ?? Path.GetFileNameWithoutExtension(document.Name), sections);
    }

    // Packs blocks greedily into parts of at most MaxTokens. A single block larger than MaxTokens stays whole.
    private List<string> SplitIntoParts(string content)
    {
        var parts = new List<string>();
        var current = "";

        foreach (var block in SplitIntoBlocks(content))
        {
            var combined = current.Length == 0 ? block : $"{current}\n\n{block}";
            if (current.Length > 0 && EstimateTokens(combined) > _maxTokens)
            {
                parts.Add(current);
                current = block;
            }
            else
            {
                current = combined;
            }
        }

        parts.Add(current);
        return parts;
    }

    // Blocks are separated by blank lines — except inside code blocks, and list items stay together with their list.
    private static List<string> SplitIntoBlocks(string content)
    {
        var blocks = new List<string>();
        var lines = new List<string>();
        var inFence = false;

        foreach (var line in content.Split('\n'))
        {
            if (IsFence(line))
            {
                inFence = !inFence;
            }

            if (!inFence && string.IsNullOrWhiteSpace(line))
            {
                AddBlock(blocks, lines);
                continue;
            }

            lines.Add(line);
        }

        AddBlock(blocks, lines);
        return blocks;
    }

    private static void AddBlock(List<string> blocks, List<string> lines)
    {
        if (lines.Count == 0)
        {
            return;
        }

        var block = string.Join('\n', lines);
        lines.Clear();

        // A list with blank lines between its items, or an indented continuation of a list item, belongs to the list.
        var continuesList = blocks.Count > 0 && IsListItem(blocks[^1]) && (IsListItem(block) || char.IsWhiteSpace(block[0]));
        if (continuesList)
        {
            blocks[^1] = $"{blocks[^1]}\n\n{block}";
        }
        else
        {
            blocks.Add(block);
        }
    }

    private static string UniqueSlug(string slug, Dictionary<string, int> usedSlugs)
    {
        var count = usedSlugs.GetValueOrDefault(slug) + 1;
        usedSlugs[slug] = count;
        return count == 1 ? slug : $"{slug}-{count}";
    }

    private static bool IsFence(string line) =>
        line.TrimStart().StartsWith("```", StringComparison.Ordinal) || line.TrimStart().StartsWith("~~~", StringComparison.Ordinal);

    private static bool IsListItem(string block) => ListItem().IsMatch(block);

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex NonAlphanumeric();

    [GeneratedRegex(@"^\s*([-*+]|\d+[.)])\s")]
    private static partial Regex ListItem();
}
