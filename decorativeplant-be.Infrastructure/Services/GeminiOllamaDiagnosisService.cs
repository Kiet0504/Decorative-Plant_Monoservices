using System.Diagnostics.CodeAnalysis;
using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace decorativeplant_be.Infrastructure.Services;

/// <summary>
/// Gemini multimodal detection + local Ollama JSON reasoning merged into <see cref="AiDiagnosisResultDto"/>.
/// </summary>
public sealed class GeminiOllamaDiagnosisService : IAiDiagnosisService, IPlantDiagnosisFromBase64Service
{
    private readonly GeminiPlantDiseaseDetectionService _geminiDetection;
    private readonly OllamaDiagnosisReasoningService _ollamaReasoning;
    private readonly IUserContentSafetyService _contentSafety;
    private readonly ILogger<GeminiOllamaDiagnosisService> _logger;

    public GeminiOllamaDiagnosisService(
        GeminiPlantDiseaseDetectionService geminiDetection,
        OllamaDiagnosisReasoningService ollamaReasoning,
        IUserContentSafetyService contentSafety,
        ILogger<GeminiOllamaDiagnosisService> logger)
    {
        _geminiDetection = geminiDetection;
        _ollamaReasoning = ollamaReasoning;
        _contentSafety = contentSafety;
        _logger = logger;
    }

    public async Task<AiDiagnosisResultDto> AnalyzePlantImageAsync(
        string imageUrl,
        string? userDescription,
        CancellationToken cancellationToken = default)
    {
        EnsureUserTextAllowed(userDescription, gardenContextText: null);

        var detection = await _geminiDetection.DetectAsync(imageUrl, userDescription, cancellationToken);
        if (detection == null)
        {
            _logger.LogWarning("Gemini detection returned no result for {ImageUrl} after retries.", imageUrl);
            ThrowGeminiUnavailable();
        }

        var (recommendations, explanation) = await _ollamaReasoning.GetAdviceAsync(
            detection,
            userDescription,
            gardenContextText: null,
            cancellationToken);

        return MergeDetectionAndAdvice(detection, recommendations, explanation);
    }

    /// <inheritdoc />
    public async Task<AiDiagnosisResultDto> AnalyzeFromBase64Async(
        string imageBase64,
        string mimeType,
        string? userDescription,
        string? gardenContextText = null,
        CancellationToken cancellationToken = default)
    {
        EnsureUserTextAllowed(userDescription, gardenContextText);

        var detection = await _geminiDetection.DetectFromBase64Async(
            imageBase64,
            mimeType,
            userDescription,
            gardenContextText,
            cancellationToken);
        if (detection == null)
        {
            _logger.LogWarning("Gemini detection returned no result for inline base64 image after retries.");
            ThrowGeminiUnavailable();
        }

        var (recommendations, explanation) = await _ollamaReasoning.GetAdviceAsync(
            detection,
            userDescription,
            gardenContextText,
            cancellationToken);

        return MergeDetectionAndAdvice(detection, recommendations, explanation);
    }

    private void EnsureUserTextAllowed(string? userDescription, string? gardenContextText)
    {
        if (_contentSafety.IsAllowed(new[] { userDescription, gardenContextText }))
        {
            return;
        }

        throw new ValidationException(_contentSafety.BlockedApiMessage);
    }

    private static AiDiagnosisResultDto MergeDetectionAndAdvice(
        GeminiDetectionResult detection,
        List<string> recommendations,
        string? explanation)
    {
        return new AiDiagnosisResultDto
        {
            Disease = detection.Disease,
            Confidence = detection.Confidence,
            Symptoms = detection.Symptoms,
            Recommendations = recommendations,
            Explanation = explanation
        };
    }

    [DoesNotReturn]
    private static void ThrowGeminiUnavailable()
    {
        throw new InvalidOperationException(
            "Gemini plant detection is unavailable (quota, overload, or network). Retry later or use the conversational assistant.");
    }
}
