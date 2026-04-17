using System.Linq;
using System.Text;
using System.Text.Json;
using decorativeplant_be.Application.Common;
using decorativeplant_be.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace decorativeplant_be.Infrastructure.Services;

/// <summary>
/// Decides whether to re-rank catalog picks after a room scan. Prefers an LLM (intent model or main <see cref="OllamaSettings.Model"/>);
/// keyword heuristics are a fast path and fallback.
/// </summary>
public sealed class OllamaRoomScanChatSuggestionIntentDetector : IRoomScanChatSuggestionIntentDetector
{
    private const string SystemPrompt =
        """
        The user already received room-based plant suggestions from our shop catalog. They are continuing in text (no new photo required).
        Decide if they want the app to show a NEW or ALTERNATIVE shortlist of products (re-rank the catalog for this room), or if they only want chat without changing picks.

        Reply with ONE JSON object only, no markdown:
        {"wantsDifferentSuggestions": true|false, "refinementNotes": string|null}

        wantsDifferentSuggestions=TRUE when they want shopping/curation help, including:
        - Asking for other, more, different, or additional plants to buy from the store; alternatives; "not these"; "something else".
        - Asking what to buy, what would fit, what you recommend purchasing for this room/space/corner, best options from the catalog, ideas for plants to order.
        - Changing constraints: cheaper, budget, pet-safe, non-toxic, easier care, smaller/taller, succulent, flowering, colorful, style.
        - Comparing which listing to choose or asking for more ideas like those picks.
        - Vietnamese examples: gợi ý khác, cây khác, nên mua cây gì, cây nào hợp phòng này, đề xuất thêm, rẻ hơn, an toàn cho thú cưng.

        wantsDifferentSuggestions=FALSE when they only want:
        - Care instructions (watering, light, repotting, soil) for a plant they mentioned without asking to refresh shop suggestions.
        - Disease/pest help without asking for different products to buy.
        - Order/shipping/store hours/account questions without asking for different plant picks.
        - Short thanks, hello, or acknowledgement with no shopping ask.

        refinementNotes: short phrase capturing constraints for the ranker (English or Vietnamese), or null if generic "alternatives" is enough.
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

    /// <summary>Model for JSON intent: dedicated intent model if set, otherwise main chat model.</summary>
    private string? ResolveIntentModel()
    {
        if (!string.IsNullOrWhiteSpace(_settings.IntentClassificationModel))
        {
            return _settings.IntentClassificationModel.Trim();
        }

        return string.IsNullOrWhiteSpace(_settings.Model) ? null : _settings.Model.Trim();
    }

    public async Task<RoomScanChatSuggestionIntentResult> DetectAsync(
        string? latestUserMessage,
        CancellationToken cancellationToken = default)
    {
        var text = latestUserMessage?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            return new RoomScanChatSuggestionIntentResult { WantsDifferentSuggestions = false };
        }

        var clipped = text.Length > 1500 ? text[..1500] + "…" : text;

        if (LooksLikeObviousNoCatalogRefresh(clipped))
        {
            _logger.LogDebug("Room-scan chat intent: obvious no refresh (thanks/greeting/shipping-only).");
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

        if (HeuristicImplicitShoppingOrCurationAsk(clipped) && !LooksLikePureCareQuestion(clipped))
        {
            _logger.LogDebug("Room-scan chat intent: implicit shopping/curation ask.");
            return new RoomScanChatSuggestionIntentResult
            {
                WantsDifferentSuggestions = true,
                RefinementNotes = clipped.Length > 400 ? clipped[..400] + "…" : clipped
            };
        }

        if (ResolveIntentModel() == null)
        {
            _logger.LogDebug("Room-scan chat intent: no model configured; heuristic fallback.");
            return HeuristicResult(clipped);
        }

        return await DetectWithLlmAsync(clipped, cancellationToken);
    }

    private async Task<RoomScanChatSuggestionIntentResult> DetectWithLlmAsync(
        string clipped,
        CancellationToken cancellationToken)
    {
        if (LooksLikePureCareQuestion(clipped) && !HeuristicImplicitShoppingOrCurationAsk(clipped))
        {
            return new RoomScanChatSuggestionIntentResult { WantsDifferentSuggestions = false };
        }

        var userBlock = new StringBuilder();
        userBlock.AppendLine("Latest user message:");
        userBlock.AppendLine(clipped);

        var model = ResolveIntentModel()!;

        try
        {
            using var doc = await _ollama.ChatJsonAsync(
                SystemPrompt,
                userBlock.ToString(),
                new OllamaJsonRequestOptions
                {
                    Model = model,
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
            WantsDifferentSuggestions =
                (HeuristicWantsDifferent(text) || HeuristicImplicitShoppingOrCurationAsk(text)) &&
                !HeuristicWantsCareNotShopping(text) &&
                !LooksLikePureCareQuestion(text),
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

    /// <summary>Short thanks / hello — no catalog refresh.</summary>
    private static bool LooksLikeObviousNoCatalogRefresh(string t)
    {
        var s = t.Trim();
        if (s.Length > 80)
        {
            return false;
        }

        var lower = s.ToLowerInvariant();
        var trivial = new[]
        {
            "thanks", "thank you", "thankyou", "thx", "ty", "ok", "okay", "great", "cool",
            "hi", "hello", "hey", "bye", "goodbye",
        };

        return trivial.Any(x => lower == x || lower.StartsWith(x + " ", StringComparison.Ordinal) || lower.StartsWith(x + ".", StringComparison.Ordinal));
    }

    /// <summary>Care-only questions that should not trigger a new shortlist (unless mixed with shopping — caller combines with ImplicitShopping).</summary>
    private static bool LooksLikePureCareQuestion(string t)
    {
        var lower = t.ToLowerInvariant();

        if (lower.Contains("how often", StringComparison.Ordinal) && lower.Contains("water", StringComparison.Ordinal))
        {
            return true;
        }

        if (lower.Contains("how do i water", StringComparison.Ordinal) ||
            lower.Contains("when should i water", StringComparison.Ordinal))
        {
            return true;
        }

        if (lower.Contains("how much light", StringComparison.Ordinal) &&
            !lower.Contains("suggest", StringComparison.Ordinal) &&
            !lower.Contains("recommend", StringComparison.Ordinal) &&
            !lower.Contains("show me", StringComparison.Ordinal))
        {
            return true;
        }

        // Vietnamese: pure care how-to
        if (lower.Contains("bao lâu tưới", StringComparison.Ordinal) ||
            lower.Contains("cách tưới", StringComparison.Ordinal) ||
            lower.Contains("cách chăm", StringComparison.Ordinal) && !lower.Contains("mua", StringComparison.Ordinal))
        {
            return true;
        }

        return false;
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

    /// <summary>Explicit "other picks" phrasing.</summary>
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

        if (shopping.Any(s => lower.Contains(s, StringComparison.Ordinal)))
        {
            return true;
        }

        var vi = new[]
        {
            "gợi ý khác", "xem thêm", "cây khác", "loại khác", "không thích", "không phải", "thay thế",
            "rẻ hơn", "dễ chăm", "cây nhỏ", "cây to", "sen đá", "xương rồng",
        };

        return vi.Any(s => lower.Contains(s, StringComparison.Ordinal));
    }

    /// <summary>Natural asks for what to buy / what fits — refresh picks without fixed phrases (narrow enough to avoid pure-care asks).</summary>
    private static bool HeuristicImplicitShoppingOrCurationAsk(string t)
    {
        var lower = t.ToLowerInvariant();

        var en = new[]
        {
            "what should i get", "what to get", "what to buy", "what plant should", "which plant should",
            "what plant would", "which plant would", "best plant for", "plants for this", "plant for this",
            "plants for my", "plant for my", "fit this room", "fit my room", "for this room", "for this space",
            "for this corner", "ideas for plants", "options for plants",
            "from your shop", "from the catalog", "from your catalog",
            "in stock", "buy a plant", "buy plants", "purchase a plant", "order a plant",
            "more plants", "other plants", "any plants for",
            "recommend a plant", "recommend plants", "recommend something for", "what do you recommend for",
            "what would you recommend", "can you recommend a plant", "can you recommend plants",
            "suggest a plant", "suggest plants", "suggest something for", "suggest for this room",
            "curate", "shortlist",
            "what else would work", "what would work here", "good choices for", "pick for me",
            "another option for", "shopping for",
        };

        if (en.Any(s => lower.Contains(s, StringComparison.Ordinal)))
        {
            return true;
        }

        // "recommend/suggest" + room/shop/buy context (avoid "recommend a schedule")
        if ((lower.Contains("recommend", StringComparison.Ordinal) || lower.Contains("suggest", StringComparison.Ordinal)) &&
            (lower.Contains("room", StringComparison.Ordinal) ||
             lower.Contains("space", StringComparison.Ordinal) ||
             lower.Contains("corner", StringComparison.Ordinal) ||
             lower.Contains("buy", StringComparison.Ordinal) ||
             lower.Contains("catalog", StringComparison.Ordinal) ||
             lower.Contains("shop", StringComparison.Ordinal) ||
             lower.Contains("plant", StringComparison.Ordinal)))
        {
            return true;
        }

        var vi = new[]
        {
            "nên mua", "nên chọn", "cây gì", "loại nào", "mua cây", "cây nào hợp",
            "phù hợp phòng", "phù hợp góc", "trong shop", "trong cửa hàng",
        };

        return vi.Any(s => lower.Contains(s, StringComparison.Ordinal));
    }
}

