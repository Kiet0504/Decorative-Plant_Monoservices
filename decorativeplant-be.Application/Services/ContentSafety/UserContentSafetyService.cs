using decorativeplant_be.Application.Common;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Common.Settings;
using Microsoft.Extensions.Options;

namespace decorativeplant_be.Application.Services.ContentSafety;

public sealed class UserContentSafetyService : IUserContentSafetyService
{
    private readonly ContentSafetySettings _settings;

    public UserContentSafetyService(IOptions<ContentSafetySettings> settings)
    {
        _settings = settings.Value;
    }

    public string BlockedChatReply { get; } =
        "This message can't be processed. Decorative Plant is only for plant care and shopping in our app. " +
        "Please ask something related to houseplants, your garden, or our store.";

    public string CrisisChatReply { get; } =
        "If you're going through a difficult time, you deserve support. Please reach out to someone you trust or a local crisis line. " +
        "In the U.S. you can call or text 988 (Suicide & Crisis Lifeline). If you're not in the U.S., search for a crisis helpline in your country.";

    public string BlockedApiMessage { get; } =
        "This request can't be processed because the text doesn't meet our content guidelines for plant-care features.";

    public ContentSafetyKind Classify(string? text) =>
        Classify(string.IsNullOrWhiteSpace(text) ? Array.Empty<string?>() : new[] { text });

    public ContentSafetyKind Classify(IEnumerable<string?> fragments)
    {
        if (!_settings.Enabled)
        {
            return ContentSafetyKind.Allowed;
        }

        var combined = string.Join(
            "\n",
            fragments.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!.Trim()));
        if (combined.Length == 0)
        {
            return ContentSafetyKind.Allowed;
        }

        var normalized = Normalize(combined);
        if (normalized.Length == 0)
        {
            return ContentSafetyKind.Allowed;
        }

        if (MatchesSelfHarm(normalized))
        {
            return ContentSafetyKind.SelfHarmCrisis;
        }

        return MatchesDisallowed(normalized) ? ContentSafetyKind.Disallowed : ContentSafetyKind.Allowed;
    }

    public bool IsAllowed(string? text) => Classify(text) == ContentSafetyKind.Allowed;

    public bool IsAllowed(IEnumerable<string?> fragments) => Classify(fragments) == ContentSafetyKind.Allowed;

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

    private static bool MatchesSelfHarm(string n)
    {
        foreach (var phrase in SelfHarmPhrases)
        {
            if (n.Contains(phrase, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchesDisallowed(string n)
    {
        foreach (var phrase in DisallowedPhrases)
        {
            if (n.Contains(phrase, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Multi-word or high-signal phrases (normalized: lower, punctuation stripped to spaces).</summary>
    private static readonly string[] SelfHarmPhrases =
    {
        "how to kill myself",
        "how can i kill myself",
        "ways to kill myself",
        "best way to kill myself",
        "i want to die",
        "want to kill myself",
        "commit suicide",
        "how to commit suicide",
        "suicide method",
        "suicide methods",
        "how to hang myself",
        "how do i hang myself",
        "slit my wrists",
        "end my life",
        "ways to die painlessly",
    };

    private static readonly string[] DisallowedPhrases =
    {
        "how to make a bomb",
        "how to build a bomb",
        "make a bomb",
        "build a bomb",
        "create a bomb",
        "pipe bomb",
        "pressure cooker bomb",
        "molotov cocktail",
        "how to make explosives",
        "make explosives",
        "thermite",
        "how to make poison gas",
        "ricin recipe",
        "how to make anthrax",
        "how to synthesize meth",
        "how to make meth",
        "child porn",
        "child pornography",
        "sex with minor",
        "sex with minors",
        "rape someone",
        "how to stalk someone",
        "track someone without their consent",
        "how to hack a bank",
        "how to steal credit card",
        "human trafficking",
        "sell weapons",
        "how to make a weapon",
    };
}
