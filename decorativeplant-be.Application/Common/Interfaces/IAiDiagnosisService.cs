namespace decorativeplant_be.Application.Common.Interfaces;

/// <summary>
/// AI service for analyzing plant images and returning disease diagnosis.
/// </summary>
public interface IAiDiagnosisService
{
    /// <summary>
    /// Analyzes a plant image and returns diagnosis (disease, confidence, symptoms, recommendations).
    /// </summary>
    Task<AiDiagnosisResultDto> AnalyzePlantImageAsync(string imageUrl, string? userDescription, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of AI plant disease analysis.
/// </summary>
public class AiDiagnosisResultDto
{
    public string Disease { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public List<string> Symptoms { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
    public string? Explanation { get; set; }
}
