namespace decorativeplant_be.Application.Features.AiChat;

/// <summary>
/// Heuristic: user wants real Decorative Plant listings from their saved profile (no room-scan thread yet).
/// </summary>
public static class ProfileShopRecommendationIntent
{
    /// <summary>
    /// True when we should run catalog ranking using a synthetic room profile from <see cref="Domain.Entities.UserAccount"/>.
    /// </summary>
    public static bool WantsProfileBasedShopPicks(
        string? latestUserMessage,
        bool conversationIncludesRoomScanCatalog,
        bool hasRoomScanFollowUpPayload)
    {
        if (hasRoomScanFollowUpPayload || conversationIncludesRoomScanCatalog)
        {
            return false;
        }

        var t = latestUserMessage?.Trim() ?? string.Empty;
        if (t.Length == 0)
        {
            return false;
        }

        var lower = t.ToLowerInvariant();

        if (LooksLikePureCareOrDiagnostics(lower))
        {
            return false;
        }

        // Strong match: user is clearly asking for shop/catalog picks (no need for the word "profile").
        if (LooksLikeShopProductRequest(lower))
        {
            return true;
        }

        var shopping =
            lower.Contains("recommend", StringComparison.Ordinal) ||
            lower.Contains("suggestion", StringComparison.Ordinal) ||
            lower.Contains("what to buy", StringComparison.Ordinal) ||
            lower.Contains("what should i get", StringComparison.Ordinal) ||
            lower.Contains("plants for me", StringComparison.Ordinal) ||
            lower.Contains("from your shop", StringComparison.Ordinal) ||
            lower.Contains("from the catalog", StringComparison.Ordinal) ||
            lower.Contains("your catalog", StringComparison.Ordinal) ||
            lower.Contains("in stock", StringComparison.Ordinal) ||
            (lower.Contains("plant") &&
                (lower.Contains("sell") || lower.Contains("carry") || lower.Contains("buy") || lower.Contains("shop")));

        var profileCue =
            lower.Contains("profile", StringComparison.Ordinal) ||
            lower.Contains("my preferences", StringComparison.Ordinal) ||
            lower.Contains("based on my", StringComparison.Ordinal) ||
            lower.Contains("for me", StringComparison.Ordinal) ||
            lower.Contains("for my ", StringComparison.Ordinal) ||
            lower.Contains("my office", StringComparison.Ordinal) ||
            lower.Contains("my desk", StringComparison.Ordinal) ||
            lower.Contains("my room", StringComparison.Ordinal) ||
            lower.Contains("my space", StringComparison.Ordinal);

        if (shopping && (profileCue || lower.Contains("shop", StringComparison.Ordinal) ||
                         lower.Contains("catalog", StringComparison.Ordinal)))
        {
            return true;
        }

        // Short asks: "recommend some plants" / "what should I get for my desk"
        if ((lower.Contains("recommend", StringComparison.Ordinal) ||
             lower.Contains("suggest", StringComparison.Ordinal)) &&
            (lower.Contains("plant", StringComparison.Ordinal) || lower.Contains("pick", StringComparison.Ordinal)))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// User wants real listings: recommend/suggest + product/shopping vocabulary (covers phrases that
    /// omit both "profile" and "shop", e.g. "recommend something for my office").
    /// </summary>
    private static bool LooksLikeShopProductRequest(string lower)
    {
        var asksRecOrSuggest =
            lower.Contains("recommend", StringComparison.Ordinal) ||
            lower.Contains("suggestion", StringComparison.Ordinal) ||
            lower.Contains("suggest", StringComparison.Ordinal);

        if (!asksRecOrSuggest)
        {
            return false;
        }

        return lower.Contains("plant", StringComparison.Ordinal) ||
               lower.Contains("catalog", StringComparison.Ordinal) ||
               lower.Contains("shop", StringComparison.Ordinal) ||
               lower.Contains("product", StringComparison.Ordinal) ||
               lower.Contains("listing", StringComparison.Ordinal) ||
               lower.Contains("buy", StringComparison.Ordinal) ||
               lower.Contains("stock", StringComparison.Ordinal) ||
               lower.Contains("from your", StringComparison.Ordinal) ||
               lower.Contains("your store", StringComparison.Ordinal) ||
               lower.Contains("pick ", StringComparison.Ordinal) ||
               lower.Contains("pick?", StringComparison.Ordinal) ||
               lower.Contains("for my ", StringComparison.Ordinal) ||
               lower.Contains("my office", StringComparison.Ordinal) ||
               lower.Contains("my desk", StringComparison.Ordinal) ||
               (lower.Contains("beginner", StringComparison.Ordinal) &&
                lower.Contains("plant", StringComparison.Ordinal)) ||
               (lower.Contains("what", StringComparison.Ordinal) &&
                lower.Contains("recommend", StringComparison.Ordinal)) ||
               (lower.Contains("can you", StringComparison.Ordinal) &&
                (lower.Contains("recommend", StringComparison.Ordinal) ||
                 lower.Contains("suggest", StringComparison.Ordinal)));
    }

    private static bool LooksLikePureCareOrDiagnostics(string lower)
    {
        if (lower.Contains("yellow leaves", StringComparison.Ordinal) ||
            lower.Contains("brown tips", StringComparison.Ordinal) ||
            lower.Contains("root rot", StringComparison.Ordinal) ||
            lower.Contains("pest", StringComparison.Ordinal) ||
            lower.Contains("bug", StringComparison.Ordinal) ||
            lower.Contains("drooping", StringComparison.Ordinal))
        {
            return true;
        }

        if (lower.Contains("how often", StringComparison.Ordinal) && lower.Contains("water", StringComparison.Ordinal) &&
            !lower.Contains("buy", StringComparison.Ordinal) && !lower.Contains("shop", StringComparison.Ordinal))
        {
            return true;
        }

        return false;
    }
}
