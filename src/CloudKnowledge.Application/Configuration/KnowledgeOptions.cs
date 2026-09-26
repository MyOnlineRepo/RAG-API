using System.ComponentModel.DataAnnotations;

namespace CloudKnowledge.Application.Configuration;

public sealed class KnowledgeOptions
{
    public const string SectionName = "Knowledge";

    [Required(ErrorMessage = "Knowledge:DocsPath is required.")]
    public string DocsPath { get; set; } = "docs";

    [Required(ErrorMessage = "Knowledge:EmbeddingCachePath is required.")]
    public string EmbeddingCachePath { get; set; } = ".cache/embeddings.json";
}
