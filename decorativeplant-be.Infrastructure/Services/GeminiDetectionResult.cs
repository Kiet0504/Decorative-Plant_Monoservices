namespace decorativeplant_be.Infrastructure.Services;

/// <summary>Structured output from Gemini disease-detection call (no care advice).</summary>
public sealed class GeminiDetectionResult
{
    public string Disease { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public List<string> Symptoms { get; set; } = new();
    /// <summary>Optional short context for the local reasoning step.</summary>
    public string? Notes { get; set; }
}
