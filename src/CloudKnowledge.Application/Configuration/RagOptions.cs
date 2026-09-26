using System.ComponentModel.DataAnnotations;

namespace CloudKnowledge.Application.Configuration;

public sealed class RagOptions
{
    public const string SectionName = "Rag";

    [Range(1, 20, ErrorMessage = "Rag:TopK must be between 1 and 20.")]
    public int TopK { get; set; } = 5;

    [Range(0.0, 1.0, ErrorMessage = "Rag:MinScore must be between 0.0 and 1.0.")]
    public double MinScore { get; set; }
}
