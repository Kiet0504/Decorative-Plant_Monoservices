using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Common.Settings;
using Microsoft.Extensions.Options;

namespace decorativeplant_be.Application.Services.PlantAssistantScope;

/// <summary>
/// Keyword-based domain gate: off-topic phrases fail only when no plant/app signal is present.
/// Images, room-scan follow-up, and garden-plant focus bypass the gate (context is already plant-related).
/// </summary>
public sealed class PlantAssistantScopeService : IPlantAssistantScopeService
{
    private readonly PlantAssistantScopeSettings _settings;

    public PlantAssistantScopeService(IOptions<PlantAssistantScopeSettings> settings)
    {
        _settings = settings.Value;
    }

    public string OutOfScopeReply { get; } =
        "I only help with houseplants, the Decorative Plant shop (orders, listings, branches), room-based plant suggestions, " +
        "My Garden care, watering schedules, and plant health or disease questions. " +
        "Ask me something in those areas and I will do my best.";

    public string OutOfScopeApiMessage { get; } =
        "That text does not look related to plant care or this app. Please describe something about your plant or garden.";

    public bool IsInScopeForPlainUserText(string? text)
    {
        if (!_settings.Enabled)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        var n = Normalize(text);
        if (n.Length == 0)
        {
            return true;
        }

        if (CountMatches(n, PlantAndAppSignals) > 0)
        {
            return true;
        }

        return CountMatches(n, OffTopicSignals) == 0;
    }

    public bool IsInScopeForChat(
        string combinedUserMessages,
        bool hasAttachedImage,
        bool hasRoomScanFollowUp,
        bool hasGardenPlantFocus,
        bool hasArPreviewContext = false,
        bool hasProductListingContext = false)
    {
        if (!_settings.Enabled)
        {
            return true;
        }

        if (hasAttachedImage || hasRoomScanFollowUp || hasGardenPlantFocus || hasArPreviewContext ||
            hasProductListingContext)
        {
            return true;
        }

        var n = Normalize(combinedUserMessages);
        if (n.Length == 0)
        {
            return true;
        }

        if (LooksLikeShortGreetingOrAck(n))
        {
            return true;
        }

        var plantScore = CountMatches(n, PlantAndAppSignals);
        var offScore = CountMatches(n, OffTopicSignals);

        if (plantScore > 0)
        {
            return true;
        }

        if (offScore > 0)
        {
            return false;
        }

        // No strong signal: allow (model + system prompt steer; avoids blocking vague plant questions).
        return true;
    }

    private static int CountMatches(string normalized, string[] phrases)
    {
        var n = 0;
        foreach (var p in phrases)
        {
            if (normalized.Contains(p, StringComparison.Ordinal))
            {
                n++;
            }
        }

        return n;
    }

    private static bool LooksLikeShortGreetingOrAck(string normalized)
    {
        if (normalized.Length > 48)
        {
            return false;
        }

        var t = normalized.Trim();
        ReadOnlySpan<string> ok =
        [
            "hi", "hello", "hey", "thanks", "thank you", "ok", "okay", "yes", "no", "bye", "good morning",
            "good afternoon", "good evening", "help", "help me",
        ];

        foreach (var s in ok)
        {
            if (t == s || t.StartsWith(s + " ", StringComparison.Ordinal) || t.StartsWith(s + ",", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string Normalize(string s)
    {
        var lower = s.ToLowerInvariant();
        var sb = new System.Text.StringBuilder(lower.Length);
        foreach (var c in lower)
        {
            if (char.IsLetterOrDigit(c) || char.IsWhiteSpace(c))
            {
                sb.Append(c);
            }
            else
            {
                sb.Append(' ');
            }
        }

        return string.Join(' ', sb.ToString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>Houseplants, app, commerce, environment tied to growing.</summary>
    private static readonly string[] PlantAndAppSignals =
    [
        "plant", "plants", "houseplant", "indoor", "outdoor", "patio", "leaf", "leaves", "soil", "water", "watering",
        "overwater", "underwater", "root", "roots", "repot", "pot", "pots", "fertilizer", "fertilise", "fertilize",
        "humidity", "mist", "spray", "light", "sun", "shade", "window", "grow", "growing", "growth", "prune", "trim",
        "yellow", "brown", "black spot", "wilting", "wilt", "drooping", "pest", "pests", "bug", "bugs", "mite",
        "spider mite", "fungus", "mold", "rot", "disease", "diagnos", "sick plant", "toxic", "pet safe", "cat", "dog",
        "succulent", "cactus", "orchid", "fern", "monstera", "pothos", "snake plant", "zz plant", "calathea", "ficus",
        "bonsai", "herb", "seedling", "cutting", "propagation", "garden", "my garden", "schedule", "reminder", "calendar",
        "room scan", "corner", "listing", "shop", "store", "branch", "order", "delivery", "cart", "price", "buy",
        "pick", "suggestion", "catalog", "stock", "inventory", "care tip", "care advice", "hardiness", "zone",
        "temperature", "season", "winter", "summer", "bloom", "flower", "fruit", "variegated",
    ];

    /// <summary>Clear non-plant general tasks (only used when no plant signal matched).</summary>
    private static readonly string[] OffTopicSignals =
    [
        "write code", "python script", "javascript", "typescript", "java program", "sql query", "regex", "leetcode",
        "compile", "docker", "kubernetes", "linux command", "terminal command", "hack ", "hacking ", "exploit",
        "bitcoin", "ethereum", "crypto wallet", "stock price", "forex",
        "translate this", "translate to", "essay about", "homework", "solve for x", "derivative of", "integral of",
        "president of", "election", "political party", "religion", "who won the super bowl",
        "movie review", "netflix", "celebrity gossip",
        "recipe for", "cook ", "baking ", "restaurant recommend",
        "legal advice", "tax advice", "medical diagnosis", "prescription drug",
    ];
}
