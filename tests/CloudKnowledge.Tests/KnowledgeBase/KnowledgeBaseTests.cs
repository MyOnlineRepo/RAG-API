using System.Text.RegularExpressions;

namespace CloudKnowledge.Tests.KnowledgeBase;

/// <summary>
/// Guards the deliberate test cases of the knowledge base (SPEC §4.3): structure the chunker relies on,
/// facts that must stay split across files, and facts that must never be documented.
/// </summary>
public sealed class KnowledgeBaseTests
{
    private static readonly string[] ExpectedFiles =
    [
        "api.md", "architecture.md", "authentication.md", "container-apps.md", "deployment.md",
        "monitoring.md", "security.md", "storage.md", "troubleshooting.md",
    ];

    public static TheoryData<string> Files => new(ExpectedFiles);

    public static TheoryData<string> ForbiddenPatterns => new(
        @"\b\d+\s?(KB|MB|GB|TB)\b",
        @"max(imum)? (upload|file) size",
        @"size limit",
        @"\bSLA\b",
        @"\d{2}(\.\d+)?\s?%\s*(availability|uptime)",
        @"retention",
        @"retained for");

    [Fact]
    public void Contains_exactly_the_expected_documents()
    {
        var actual = Directory.GetFiles(DocsPath, "*.md").Select(Path.GetFileName).Order();

        Assert.Equal(ExpectedFiles, actual);
    }

    [Theory]
    [MemberData(nameof(Files))]
    public void Document_has_one_title_and_at_least_three_sections(string file)
    {
        var lines = LinesOutsideCodeBlocks(file);

        Assert.Single(lines, line => line.StartsWith("# "));
        Assert.True(lines.Count(line => line.StartsWith("## ")) >= 3, $"{file} needs at least three H2 sections.");
    }

    [Theory]
    [MemberData(nameof(Files))]
    public void Document_length_is_within_range(string file)
    {
        var words = Regex.Matches(Read(file), @"\S+").Count;

        Assert.InRange(words, 400, 1200);
    }

    [Theory]
    [MemberData(nameof(ForbiddenPatterns))]
    public void Deliberately_missing_facts_are_not_documented(string pattern)
    {
        var hits = ExpectedFiles
            .SelectMany(file => Regex.Matches(Read(file), pattern, RegexOptions.IgnoreCase)
                .Select(match => $"{file}: '{match.Value}'"))
            .ToList();

        Assert.True(hits.Count == 0, $"Forbidden pattern '{pattern}' found: {string.Join(", ", hits)}");
    }

    [Fact]
    public void Split_facts_stay_split()
    {
        Assert.Contains("## HTTP 503 after deployment", Read("troubleshooting.md"));
        Assert.Contains("/health/ready", Read("monitoring.md"));
        Assert.DoesNotContain("/health/", Read("troubleshooting.md"));
        Assert.DoesNotContain("/health/", Read("container-apps.md"));

        Assert.Contains("Azure Blob Storage", Read("storage.md"));
        Assert.DoesNotContain("Managed Identity", Read("storage.md"), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Blob Storage access uses Managed Identity", Read("security.md"));
    }

    private static readonly string DocsPath = FindDocsPath();

    private static string Read(string file) => File.ReadAllText(Path.Combine(DocsPath, file));

    private static List<string> LinesOutsideCodeBlocks(string file)
    {
        var inCodeBlock = false;
        var lines = new List<string>();
        foreach (var line in File.ReadLines(Path.Combine(DocsPath, file)))
        {
            if (line.StartsWith("```"))
            {
                inCodeBlock = !inCodeBlock;
            }
            else if (!inCodeBlock)
            {
                lines.Add(line);
            }
        }

        return lines;
    }

    private static string FindDocsPath()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "CloudKnowledge.sln")))
            {
                return Path.Combine(dir.FullName, "docs");
            }
        }

        throw new InvalidOperationException("Repository root (CloudKnowledge.sln) not found.");
    }
}
