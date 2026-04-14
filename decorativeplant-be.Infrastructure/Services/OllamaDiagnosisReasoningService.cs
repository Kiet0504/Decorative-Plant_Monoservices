using System.Text.Json;
using decorativeplant_be.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace decorativeplant_be.Infrastructure.Services;

/// <summary>
/// Local Ollama JSON step: care recommendations and explanation from detection output (no image).
/// </summary>
public sealed class OllamaDiagnosisReasoningService
{
    private readonly IOllamaClient _ollama;
    private readonly ILogger<OllamaDiagnosisReasoningService> _logger;

    public OllamaDiagnosisReasoningService(
        IOllamaClient ollama,
        ILogger<OllamaDiagnosisReasoningService> logger)
    {
        _ollama = ollama;
        _logger = logger;
    }

    public async Task<(List<string> Recommendations, string? Explanation)> GetAdviceAsync(
        GeminiDetectionResult detection,
        string? userDescription,
        string? gardenContextText = null,
        CancellationToken cancellationToken = default)
    {
        const string systemPrompt =
            "You are a plant care advisor. You receive machine detection JSON from an automated vision model (not a medical diagnosis). " +
            "Respond with JSON only (no markdown, no code fences) with this exact structure: " +
            "{\"recommendations\": [\"short actionable step\", ...], \"explanation\": \"brief plain-language summary\"}. " +
            "Give practical home care, isolation or pruning when relevant, and when to consult an expert. " +
            "When optional app/garden context is provided, use it to explain plausible contributing factors (e.g. watering vs schedule, humidity, light, recent repot, past issues) and tailor recommendations. Do not invent facts not present in the context or detection. " +
            "If confidence in the detection is low, say so in the explanation and avoid overconfident claims.";

        var trimmedContext = TrimGardenContext(gardenContextText);
        var userPrompt =
            "Detection JSON:\n" +
            JsonSerializer.Serialize(detection) +
            "\nUser description (may be None): " + (userDescription ?? "None") +
            (string.IsNullOrEmpty(trimmedContext)
                ? ""
                : "\n\nApp / garden context (use to personalize causes and next steps; may be incomplete):\n" + trimmedContext);

        try
        {
            using var doc = await _ollama.ChatJsonAsync(systemPrompt, userPrompt, cancellationToken);
            var root = doc.RootElement;
            var recommendations = root.TryGetProperty("recommendations", out var r) && r.ValueKind == JsonValueKind.Array
                ? r.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => !string.IsNullOrEmpty(x)).ToList()
                : new List<string>();
            var explanation = root.TryGetProperty("explanation", out var e) ? e.GetString() : null;
            return (recommendations, explanation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ollama diagnosis reasoning failed.");
            return (new List<string>(), "Local reasoning is temporarily unavailable. Detection above is from the vision model only; consult a plant expert if unsure.");
        }
    }

    private static string TrimGardenContext(string? gardenContextText)
    {
        if (string.IsNullOrWhiteSpace(gardenContextText))
        {
            return string.Empty;
        }

        var s = gardenContextText.Trim();
        const int max = 12000;
        return s.Length <= max ? s : s[..max] + "\n…(truncated)";
    }
}
