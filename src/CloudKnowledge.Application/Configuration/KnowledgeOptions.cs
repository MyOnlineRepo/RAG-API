using System.ComponentModel.DataAnnotations;

namespace CloudKnowledge.Application.Configuration;

public sealed class KnowledgeOptions
{
    public const string SectionName = "Knowledge";

    /// <summary>Folder with the Markdown knowledge base. Relative paths resolve against the app's base directory.</summary>
    [Required(ErrorMessage = "Knowledge:DocsPath is required.")]
    public string DocsPath { get; set; } = "docs";

    /// <summary>JSON file for cached embeddings. Relative paths resolve against the app's base directory.</summary>
    [Required(ErrorMessage = "Knowledge:EmbeddingCachePath is required.")]
    public string EmbeddingCachePath { get; set; } = ".cache/embeddings.json";

    /// <summary>Build the index once when the API starts.</summary>
    public bool IndexOnStartup { get; set; } = true;
}
