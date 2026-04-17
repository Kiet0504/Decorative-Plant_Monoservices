using System.Text;
using System.Text.Json;
using decorativeplant_be.Application.Common;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.AiChat;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace decorativeplant_be.Infrastructure.Services;

/// <summary>
/// Detects shop/catalog recommendation intent for general chat (no room-scan context).
/// Mirrors <see cref="OllamaRoomScanChatSuggestionIntentDetector"/>: heuristics first, LLM JSON when configured.
/// </summary>
public sealed class OllamaAiChatProfileShopIntentDetector : IAiChatProfileShopIntentDetector
{
    private const string LlmSystemPrompt =
        """
        You classify the latest user message for a houseplant app "Decorative Plant" with an online shop.

        Decide if they want SHOPPING / PRODUCT recommendations (what to buy from the store, catalog picks, in-stock listings, purchase ideas) versus general plant chat without a purchase focus.

        Reply with ONE JSON object only, no markdown:
        {"wantsProfileShop": true|false}

        wantsProfileShop=TRUE when they want to browse, buy, or be told what products to get — e.g. recommend plants from your shop, what should I order, suggestions from the catalog, plants for my desk/office with buying intent, budget picks, beginners' plants to purchase, gift ideas from the store, "what do you sell", alternatives to buy.

        wantsProfileShop=FALSE when they only want care advice (watering, light, repotting), disease ID, pest help, comparing species in theory, thanks, hello, order tracking without asking what to buy, or questions that do not ask for store products.

        Mixed messages: if they clearly ask what to buy or for shop picks, TRUE. If only care, FALSE.
        """;

    private readonly IOllamaClient _ollama;
    private readonly OllamaSettings _settings;
    private readonly ILogger<OllamaAiChatProfileShopIntentDetector> _logger;

    public OllamaAiChatProfileShopIntentDetector(
        IOllamaClient ollama,
        IOptions<OllamaSettings> settings,
        ILogger<OllamaAiChatProfileShopIntentDetector> logger)
    {
        _ollama = ollama;
        _settings = settings.Value;
        _logger = logger;
    }

    private string? ResolveIntentModel()
    {
        if (!string.IsNullOrWhiteSpace(_settings.IntentClassificationModel))
        {
            return _settings.IntentClassificationModel.Trim();
        }

        return string.IsNullOrWhiteSpace(_settings.Model) ? null : _settings.Model.Trim();
    }

    /// <inheritdoc />
    public async Task<bool> WantsProfileShopCatalogAsync(
        string? latestUserMessage,
        CancellationToken cancellationToken = default)
    {
        var text = latestUserMessage?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            return false;
        }

        var clipped = text.Length > 1500 ? text[..1500] + "…" : text;

        if (LooksLikeGreetingOrAckOnly(clipped))
        {
            _logger.LogDebug("Profile shop intent: greeting/ack only.");
            return false;
        }

        // Strong keyword path (same rules as ProfileShopRecommendationIntent, non-room-scan).
        if (ProfileShopRecommendationIntent.WantsProfileBasedShopPicks(clipped, false, false))
        {
            _logger.LogDebug("Profile shop intent: heuristic keyword match.");
            return true;
        }

        if (LooksLikePureCareOnly(clipped))
        {
            _logger.LogDebug("Profile shop intent: pure care question heuristic.");
            return false;
        }

        if (ResolveIntentModel() == null)
        {
            _logger.LogDebug("Profile shop intent: no Ollama model configured; LLM skip.");
            return false;
        }

        return await DetectWithLlmAsync(clipped, cancellationToken);
    }

    private async Task<bool> DetectWithLlmAsync(string clipped, CancellationToken cancellationToken)
    {
        var userBlock = new StringBuilder();
        userBlock.AppendLine("Latest user message:");
        userBlock.AppendLine(clipped);

        var model = ResolveIntentModel()!;

        try
        {
            using var doc = await _ollama.ChatJsonAsync(
                LlmSystemPrompt,
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
            var wants = TryReadWantsProfileShop(root);
            if (wants.HasValue)
            {
                _logger.LogInformation("Profile shop intent LLM: wantsProfileShop={Wants}", wants.Value);
                return wants.Value;
            }

            _logger.LogWarning("Profile shop intent JSON missing wantsProfileShop; using false.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Profile shop intent LLM failed; using false.");
        }

        return false;
    }

    private static bool? TryReadWantsProfileShop(JsonElement root)
    {
        ReadOnlySpan<string> names =
        [
            "wantsProfileShop",
            "wants_profile_shop",
            "profileShop",
            "wants_shop",
        ];

        foreach (var name in names)
        {
            if (root.TryGetProperty(name, out var el) && el.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                return el.GetBoolean();
            }
        }

        return null;
    }

    private static bool LooksLikeGreetingOrAckOnly(string lower)
    {
        var t = lower.Trim().ToLowerInvariant();
        if (t.Length > 80)
        {
            return false;
        }

        return t is "hi" or "hello" or "hey" or "thanks" or "thank you" or "ok" or "okay" or "bye"
               || t.StartsWith("thank you", StringComparison.Ordinal)
               || t.StartsWith("thanks ", StringComparison.Ordinal);
    }

    private static bool LooksLikePureCareOnly(string text)
    {
        var lower = text.ToLowerInvariant();
        if (lower.Contains("yellow leaves", StringComparison.Ordinal) ||
            lower.Contains("brown tips", StringComparison.Ordinal) ||
            lower.Contains("root rot", StringComparison.Ordinal) ||
            lower.Contains("how often", StringComparison.Ordinal) && lower.Contains("water", StringComparison.Ordinal) &&
            !lower.Contains("buy", StringComparison.Ordinal) && !lower.Contains("shop", StringComparison.Ordinal))
        {
            return true;
        }

        return false;
    }
}
