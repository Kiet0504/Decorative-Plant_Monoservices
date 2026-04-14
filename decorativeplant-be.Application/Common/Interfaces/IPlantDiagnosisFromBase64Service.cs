namespace decorativeplant_be.Application.Common.Interfaces;

/// <summary>
/// Gemini + Ollama disease pipeline using inline image bytes (e.g. from chat uploads), not a URL.
/// </summary>
public interface IPlantDiagnosisFromBase64Service
{
    /// <summary>Raw base64 (no data: prefix). MIME e.g. image/jpeg.</summary>
    /// <param name="gardenContextText">
    /// Optional My Garden context (profile + focus plant schedules, diary, milestones, past checks) so detection and reasoning can relate causes to care history.
    /// </param>
    Task<AiDiagnosisResultDto> AnalyzeFromBase64Async(
        string imageBase64,
        string mimeType,
        string? userDescription,
        string? gardenContextText = null,
        CancellationToken cancellationToken = default);
}
