using System.ComponentModel.DataAnnotations;

namespace CloudKnowledge.Application.Configuration;

public sealed class AzureOpenAIOptions : IValidatableObject
{
    public const string SectionName = "AzureOpenAI";

    [Required(ErrorMessage = "AzureOpenAI:Endpoint is required. Set it via user secrets or environment variables.")]
    public string Endpoint { get; set; } = "";

    [Required(ErrorMessage = "AzureOpenAI:ChatDeployment is required. Set it via user secrets or environment variables.")]
    public string ChatDeployment { get; set; } = "";

    [Required(ErrorMessage = "AzureOpenAI:EmbeddingDeployment is required.")]
    public string EmbeddingDeployment { get; set; } = "";

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Endpoint))
        {
            yield break;
        }

        if (!Uri.TryCreate(Endpoint, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            yield return new ValidationResult(
                $"AzureOpenAI:Endpoint must be an absolute https URI, but was '{Endpoint}'.",
                [nameof(Endpoint)]);
        }
    }
}
