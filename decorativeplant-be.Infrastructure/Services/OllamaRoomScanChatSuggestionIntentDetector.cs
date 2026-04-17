using System.Linq;
using System.Text;
using System.Text.Json;
using decorativeplant_be.Application.Common;
using decorativeplant_be.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace decorativeplant_be.Infrastructure.Services;

/// <summary>
/// Uses the configured small Ollama JSON model (<see cref="OllamaSettings.IntentClassificationModel"/>) when set; otherwise keyword heuristics.
/// </summary>
public sealed class OllamaRoomScanChatSuggestionIntentDetector : IRoomScanChatSuggestionIntentDetector
{
    private const string SystemPrompt =
        """
        You classify the user's latest message in a plant shopping chat after they received room-based catalog suggestions.
        Reply with ONE JSON object only, no markdown:
        {"wantsDifferentSuggestions": true|false, "refinementNotes": string|null}
        wantsDifferentSuggestions=true when they want other, alternative, or additional product options from the shop
        (e.g. different plants, show me more, not these, something else, cheaper, pet-safe, easier care, taller, smaller, succulent, flowering).
        wantsDifferentSuggestions=false when they ask how to care for a plant, compare care, watering schedule, identify a species from the name,
        order/shipping questions, or general chat not asking to change the product shortlist.
        refinementNotes: short English phrase for the catalog ranker (constraints or style), or null if none beyond "alternatives".
        """;

    private readonly IOllamaClient _ollama;
    private readonly OllamaSettings _settings;
    private readonly ILogger<OllamaRoomScanChatSuggestionIntentDetector> _logger;

    public OllamaRoomScanChatSuggestionIntentDetector(
        IOllamaClient ollama,
        IOptions<OllamaSettings> settings,
        ILogger<OllamaRoomScanChatSuggestionIntentDetector> logger)
    {
        _ollama = ollama;
        _settings = settings.Value;
        _logger = logger;
    }

    public Task<RoomScanChatSuggestionIntentResult> DetectAsync(
        string? latestUserMessage,
        CancellationToken cancellationToken = default)
    {
        var text = latestUserMessage?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            return Task.FromResult(new RoomScanChatSuggestionIntentResult { WantsDifferentSuggestions = false });
        }

        if (string.IsNullOrWhiteSpace(_settings.IntentClassificationModel))
        {
            return Task.FromResult(HeuristicResult(text));
        }

        return DetectWithLlmAsync(text, cancellationToken);
    }

    private async Task<RoomScanChatSuggestionIntentResult> DetectWithLlmAsync(
        string text,
        CancellationToken cancellationToken)
    {
        var clipped = text.Length > 1500 ? text[..1500] + "…" : text;

        if (LooksLikeCareQuestionOnly(clipped))
        {
            return new RoomScanChatSuggestionIntentResult { WantsDifferentSuggestions = false };
        }

        if (HeuristicWantsDifferent(clipped) && !HeuristicWantsCareNotShopping(clipped))
        {
            _logger.LogDebug("Room-scan chat intent: keyword wants different suggestions.");
            return new RoomScanChatSuggestionIntentResult
            {
                WantsDifferentSuggestions = true,
                RefinementNotes = clipped.Length > 400 ? clipped[..400] + "…" : clipped
            };
        }

        var userBlock = new StringBuilder();
        userBlock.AppendLine("Latest user message:");
        userBlock.AppendLine(clipped);

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
            var wants = TryReadWantsDifferent(root);
            var notes = TryReadRefinementNotes(root);
            if (wants.HasValue)
            {
                _logger.LogDebug("Room-scan chat intent LLM: wantsDifferent={Wants}", wants.Value);
                return new RoomScanChatSuggestionIntentResult
                {
                    WantsDifferentSuggestions = wants.Value,
                    RefinementNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()
                };
            }

            _logger.LogWarning("Room-scan chat intent JSON missing wantsDifferentSuggestions; using heuristic.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Room-scan chat intent LLM failed; using heuristic.");
        }

        return HeuristicResult(clipped);
    }

    private static RoomScanChatSuggestionIntentResult HeuristicResult(string text) =>
        new()
        {
            WantsDifferentSuggestions = HeuristicWantsDifferent(text) && !HeuristicWantsCareNotShopping(text),
            RefinementNotes = text.Length > 400 ? text[..400] + "…" : text
        };

    private static bool? TryReadWantsDifferent(JsonElement root)
    {
        ReadOnlySpan<string> names = new[]
        {
            "wantsDifferentSuggestions",
            "wants_different_suggestions",
            "differentSuggestions",
            "wantAlternatives",
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

    private static string? TryReadRefinementNotes(JsonElement root)
    {
        ReadOnlySpan<string> names = new[] { "refinementNotes", "refinement_notes", "notes", "constraints" };
        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var s = el.GetString();
            return string.IsNullOrWhiteSpace(s) ? null : s;
        }

        return null;
    }

    private static bool LooksLikeCareQuestionOnly(string lower)
    {
        var t = lower.ToLowerInvariant();
        if (t.Contains("how often", StringComparison.Ordinal) && t.Contains("water", StringComparison.Ordinal))
        {
            return true;
        }

        if (t.Contains("how do i water", StringComparison.Ordinal) ||
            t.Contains("when should i water", StringComparison.Ordinal))
        {
            return true;
        }

        return t.Contains("how much light", StringComparison.Ordinal) &&
               !t.Contains("suggest", StringComparison.Ordinal) &&
               !t.Contains("show me", StringComparison.Ordinal);
    }

    private static bool HeuristicWantsCareNotShopping(string t)
    {
        var lower = t.ToLowerInvariant();
        return lower.Contains("yellow leaves") ||
               lower.Contains("brown tips") ||
               lower.Contains("drooping") ||
               lower.Contains("root rot") ||
               lower.Contains("pest") ||
               lower.Contains("bug");
    }

    private static bool HeuristicWantsDifferent(string t)
    {
        var lower = t.ToLowerInvariant();
        var shopping = new[]
        {
            "different plant", "other plant", "another plant", "other option", "other suggestions",
            "something else", "not these", "not those", "show me more", "more option", "more choice",
            "alternatives", "alternative", "try again", "new suggestion", "other pick", "different pick",
            "else ", " instead", "cheaper", "under $", "budget", "pet safe", "pet-safe", "non-toxic",
            "easier", "low maintenance", "less fussy", "smaller", "taller", "succulent", "cactus",
            "flowering", "colorful", "another one", "what else", "any other", "other ideas",
        };

        return shopping.Any(s => lower.Contains(s, StringComparison.Ordinal));
    }
}
