using System.ComponentModel.DataAnnotations;

namespace CloudKnowledge.Application.Configuration;

public sealed class ChunkingOptions
{
    public const string SectionName = "Chunking";

    [Range(50, 2000, ErrorMessage = "Chunking:MaxTokens must be between 50 and 2000.")]
    public int MaxTokens { get; set; } = 400;
}
