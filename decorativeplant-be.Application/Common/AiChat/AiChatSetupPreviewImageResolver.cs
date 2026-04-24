using System.Globalization;

namespace decorativeplant_be.Application.Common.AiChat;

/// <summary>
/// Resolves preview image URLs for AI Hub setup idea cards.
/// Uses curated Unsplash stills (hotlink-friendly parameters) per design style; falls back to deterministic Picsum for unknown keys.
/// Replace with your own CDN/S3 assets or an image-generation pipeline when you want full brand control.
/// </summary>
public static class AiChatSetupPreviewImageResolver
{
    /// <summary>Unsplash CDN — indoor plants / interiors; license: https://unsplash.com/license</summary>
    private static readonly Dictionary<string, string> StyleToPhotoUrl = new(StringComparer.OrdinalIgnoreCase)
    {
        ["minimal"] =
            "https://images.unsplash.com/photo-1545241047-6083a3684587?auto=format&fit=crop&w=900&h=540&q=80",
        ["tropical"] =
            "https://images.unsplash.com/photo-1459411552884-841db9b3cc2a?auto=format&fit=crop&w=900&h=540&q=80",
        ["desk"] =
            "https://images.unsplash.com/photo-1497215728101-856f4ea77174?auto=format&fit=crop&w=900&h=540&q=80",
        ["pet_safe"] =
            "https://images.unsplash.com/photo-1463320726281-696a485928c7?auto=format&fit=crop&w=900&h=540&q=80",
        ["scandinavian"] =
            "https://images.unsplash.com/photo-1616046229478-9901c5536a45?auto=format&fit=crop&w=900&h=540&q=80",
        ["bohemian"] =
            "https://images.unsplash.com/photo-1484101403633-562f891dc89a?auto=format&fit=crop&w=900&h=540&q=80",
        ["biophilic"] =
            "https://images.unsplash.com/photo-1416879595882-3373a0480a5f?auto=format&fit=crop&w=900&h=540&q=80",
        ["japandi"] =
            "https://images.unsplash.com/photo-1501004318641-b39e6451bec6?auto=format&fit=crop&w=900&h=540&q=80",
        ["mid_century"] =
            "https://images.unsplash.com/photo-1586023492125-27b2c045efd7?auto=format&fit=crop&w=900&h=540&q=80",
    };

    public static string Resolve(string cacheKey, string? prompt)
    {
        var key = (cacheKey ?? string.Empty).Trim();
        var style = TryParseStyleSuffix(key);
        if (!string.IsNullOrEmpty(style) &&
            StyleToPhotoUrl.TryGetValue(style, out var url) &&
            !string.IsNullOrWhiteSpace(url))
        {
            return url!;
        }

        var seed = BuildDeterministicSeed(key, prompt);
        return $"https://picsum.photos/seed/{Uri.EscapeDataString(seed)}/900/540";
    }

    /// <summary>Expects <c>CardId:styleKey</c> from the client (style is the segment after the last colon).</summary>
    private static string? TryParseStyleSuffix(string cacheKey)
    {
        if (string.IsNullOrEmpty(cacheKey)) return null;
        var i = cacheKey.LastIndexOf(':');
        if (i <= 0 || i >= cacheKey.Length - 1) return null;
        var s = cacheKey[(i + 1)..].Trim();
        return s.Length == 0 ? null : s;
    }

    private static string BuildDeterministicSeed(string cacheKey, string? prompt)
    {
        unchecked
        {
            uint h = 2166136261u;
            var s = cacheKey + "\u001f" + (prompt ?? string.Empty);
            foreach (var ch in s)
            {
                h ^= ch;
                h *= 16777619u;
            }

            return "deco" + h.ToString("x8", CultureInfo.InvariantCulture);
        }
    }
}
