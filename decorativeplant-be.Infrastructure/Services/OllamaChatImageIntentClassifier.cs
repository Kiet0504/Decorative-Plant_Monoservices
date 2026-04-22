using System.Text;
using System.Text.Json;
using decorativeplant_be.Application.Common;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.AiChat;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace decorativeplant_be.Infrastructure.Services;

/// <summary>
/// Optional small-model (Ollama JSON) classifier for image+caption intent; falls back to <see cref="PlantChatIntentDetector"/> when disabled or on errors.
/// </summary>
public sealed class OllamaChatImageIntentClassifier : IChatImageIntentClassifier
{
    private const string SystemPrompt =
        """
        You route plant-care chat when the user attached a PHOTO (image) and may have added text.
        Reply with ONE JSON object only, no markdown:
        {"formalDiseaseCheck":true|false}
        formalDiseaseCheck=true: user wants disease, pest, mold, spots, damage, decay, "what is wrong", dying plant, or they sent a photo-only health check.
        formalDiseaseCheck=false: mainly watering/fertilizer schedules, identify species, light/sun needs, pet toxicity, repotting, humidity, pruning, shopping, or general growth tips — not primarily diagnosing visible damage.
        Use the user's language; typos are fine.
        """;

    private readonly IOllamaClient _ollama;
    private readonly OllamaSettings _settings;
    private readonly ILogger<OllamaChatImageIntentClassifier> _logger;

    public OllamaChatImageIntentClassifier(
        IOllamaClient ollama,
        IOptions<OllamaSettings> settings,
        ILogger<OllamaChatImageIntentClassifier> logger)
    {
        _ollama = ollama;
        _settings = settings.Value;
        _logger = logger;
    }

    public Task<bool> ShouldUseFormalDiagnosisPipelineAsync(
        string? lastUserText,
        bool hasImage,
        CancellationToken cancellationToken = default)
    {
        if (!hasImage)
        {
            return Task.FromResult(false);
        }

        if (string.IsNullOrWhiteSpace(_settings.IntentClassificationModel))
        {
            return Task.FromResult(PlantChatIntentDetector.ShouldUseGeminiOllamaDiagnosisPipeline(lastUserText, hasImage));
        }

        return ClassifyWithLlmAsync(lastUserText, cancellationToken);
    }

    private async Task<bool> ClassifyWithLlmAsync(string? lastUserText, CancellationToken cancellationToken)
    {
        // Photo-only / placeholder-only: do NOT force formal diagnosis.
        // Users frequently attach room/balcony photos for decor context; forcing diagnosis yields nonsense.
        if (PlantChatIntentDetector.IsPlaceholderImageCaption(lastUserText))
        {
            return false;
        }

        var text = lastUserText!.Trim();
        if (text.Length > 1500)
        {
            text = text[..1500] + "…";
        }

        // Keyword signals are deterministic and override the LLM when they clearly apply.
        if (PlantChatIntentDetector.IsDiseaseDiagnosisIntent(text))
        {
            return true;
        }

        if (PlantChatIntentDetector.LooksLikeNonDiseaseImageChat(text))
        {
            return false;
        }

        var userBlock = new StringBuilder();
        userBlock.AppendLine("The user attached an image to this message.");
        userBlock.AppendLine($"User text: {text}");

        try
        {
            using var doc = await _ollama.ChatJsonAsync(
                SystemPrompt,
                userBlock.ToString(),
                new OllamaJsonRequestOptions
                {
                    Model = _settings.IntentClassificationModel.Trim(),
                    TimeoutSeconds = Math.Clamp(_settings.IntentClassificationTimeoutSeconds, 3, 60),
                    LogFailuresAsWarnings = true,
                    Temperature = 0f,
                },
                cancellationToken);

            var root = doc.RootElement;
            var flag = TryReadFormalFlag(root);
            if (flag.HasValue)
            {
                _logger.LogDebug("LLM chat intent: formalDiseaseCheck={Flag}", flag.Value);
                return flag.Value;
            }

            _logger.LogWarning("LLM intent JSON missing formalDiseaseCheck; using keyword fallback.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LLM intent classification failed; using keyword fallback.");
        }

        return PlantChatIntentDetector.ShouldUseGeminiOllamaDiagnosisPipeline(lastUserText, hasAttachedImage: true);
    }

    private static bool? TryReadFormalFlag(JsonElement root)
    {
        ReadOnlySpan<string> names = new[]
        {
            "formalDiseaseCheck",
            "formal_disease_check",
            "formalDiagnosis",
            "useFormalDiagnosis",
        };

        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var el))
            {
                continue;
            }

            if (el.ValueKind == JsonValueKind.True)
            {
                return true;
            }

            if (el.ValueKind == JsonValueKind.False)
            {
                return false;
            }

            if (el.ValueKind == JsonValueKind.String && bool.TryParse(el.GetString(), out var b))
            {
                return b;
            }

            if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var n))
            {
                return n != 0;
            }
        }

        return null;
    }
}
