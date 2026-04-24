using System.Text.Json;
using decorativeplant_be.Application.Common.DTOs.AiChat;
using decorativeplant_be.Application.Common;
using decorativeplant_be.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace decorativeplant_be.Infrastructure.Services;

public sealed class GeminiAiContextInferenceService : IAiContextInferenceService
{
    private readonly GeminiGenerativeContentClient _gemini;
    private readonly ILogger<GeminiAiContextInferenceService> _logger;

    public GeminiAiContextInferenceService(
        GeminiGenerativeContentClient gemini,
        ILogger<GeminiAiContextInferenceService> logger)
    {
        _gemini = gemini;
        _logger = logger;
    }

    public async Task<List<AiChatContextInferenceDto>> InferAsync(
        string userText,
        IReadOnlyList<AiChatDesignStyleOptionDto> availableStyles,
        CancellationToken cancellationToken = default)
    {
        userText = (userText ?? string.Empty).Trim();
        if (userText.Length == 0) return new List<AiChatContextInferenceDto>();

        var styles = availableStyles
            .Where(s => !string.IsNullOrWhiteSpace(s.StyleKey) && !string.IsNullOrWhiteSpace(s.Label))
            .Select(s => new { styleKey = s.StyleKey.Trim(), label = s.Label.Trim() })
            .ToList();

        var systemPrompt =
            "You are a context inference engine for a plant shopping assistant.\n" +
            "Goal: extract small, high-signal context updates from ONE user message.\n" +
            "Return JSON ONLY.\n" +
            "You may infer:\n" +
            "- roomLightKey: one of [low, medium, bright] if the user mentions lighting.\n" +
            "- designStyleKey: one of the provided availableStyles.styleKey if user expresses a style.\n" +
            "If unsure, omit fields.\n" +
            "Output schema:\n" +
            "{\n" +
            "  \"roomLightKey\": { \"value\": \"low|medium|bright\", \"confidence\": 0-1, \"evidence\": \"...\" } | null,\n" +
            "  \"designStyleKey\": { \"value\": \"<styleKey>\", \"confidence\": 0-1, \"evidence\": \"...\" } | null\n" +
            "}";

        var userPrompt =
            $"User message:\n{userText}\n\n" +
            $"Available styles:\n{JsonSerializer.Serialize(styles)}";

        JsonDocument? doc = null;
        try
        {
            doc = await _gemini.ChatJsonAsync(
                systemPrompt,
                userPrompt,
                options: new OllamaJsonRequestOptions { Temperature = 0.1f, TimeoutSeconds = 30, Model = "gemini-2.5-flash" },
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Gemini context inference failed.");
            return new List<AiChatContextInferenceDto>();
        }

        var root = doc.RootElement;
        var result = new List<AiChatContextInferenceDto>();

        if (TryReadValue(root, "roomLightKey", out var roomLightValue, out var roomLightConf, out var roomLightEvidence) &&
            !string.IsNullOrWhiteSpace(roomLightValue))
        {
            var v = roomLightValue.Trim().ToLowerInvariant();
            if (v is "low" or "medium" or "bright")
            {
                result.Add(new AiChatContextInferenceDto
                {
                    Id = "room_light",
                    Label = v == "low" ? "Your room seems dim / low light" :
                            v == "bright" ? "Your room seems bright" : "Your room seems medium / indirect light",
                    Confidence = Math.Clamp(roomLightConf, 0, 1),
                    Evidence = roomLightEvidence,
                    ContextPatch = new AiChatContextPatchEnvelopeDto
                    {
                        Version = 1,
                        Patch = new AiChatContextPatchDto
                        {
                            RoomContext = new AiChatRoomContextPatchDto { LightKey = v }
                        }
                    }
                });
            }
        }

        if (TryReadValue(root, "designStyleKey", out var styleValue, out var styleConf, out var styleEvidence) &&
            !string.IsNullOrWhiteSpace(styleValue))
        {
            var match = styles.FirstOrDefault(s => string.Equals(s.styleKey, styleValue.Trim(), StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                result.Add(new AiChatContextInferenceDto
                {
                    Id = "design_style",
                    Label = $"Style preference: {match.label}",
                    Confidence = Math.Clamp(styleConf, 0, 1),
                    Evidence = styleEvidence,
                    ContextPatch = new AiChatContextPatchEnvelopeDto
                    {
                        Version = 1,
                        Patch = new AiChatContextPatchDto
                        {
                            DesignContext = new AiChatDesignContextPatchDto { StyleKey = match.styleKey }
                        }
                    }
                });
            }
        }

        return result;
    }

    private static bool TryReadValue(
        JsonElement root,
        string prop,
        out string? value,
        out double confidence,
        out string? evidence)
    {
        value = null;
        confidence = 0;
        evidence = null;

        if (!root.TryGetProperty(prop, out var el) || el.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return false;
        }

        if (el.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (el.TryGetProperty("value", out var vEl) && vEl.ValueKind == JsonValueKind.String)
        {
            value = vEl.GetString();
        }
        if (el.TryGetProperty("confidence", out var cEl) && cEl.ValueKind == JsonValueKind.Number)
        {
            _ = cEl.TryGetDouble(out confidence);
        }
        if (el.TryGetProperty("evidence", out var eEl) && eEl.ValueKind == JsonValueKind.String)
        {
            evidence = eEl.GetString();
        }

        return value != null;
    }
}

