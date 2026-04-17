using decorativeplant_be.Application.Common;

namespace decorativeplant_be.Application.Common.Interfaces;

/// <summary>
/// Deterministic checks on user-supplied text before LLM calls. Does not scan server-built prompts that include catalog/taxonomy JSON.
/// </summary>
public interface IUserContentSafetyService
{
    /// <summary>Classify combined user fragments (null/whitespace ignored).</summary>
    ContentSafetyKind Classify(IEnumerable<string?> fragments);

    /// <summary>Convenience for a single message.</summary>
    ContentSafetyKind Classify(string? text);

    /// <summary>True if all fragments are acceptable (null/whitespace fragments are ignored).</summary>
    bool IsAllowed(IEnumerable<string?> fragments);

    /// <summary>Convenience for a single message.</summary>
    bool IsAllowed(string? text);

    /// <summary>Standard short reply when content is blocked (chat UI).</summary>
    string BlockedChatReply { get; }

    /// <summary>Message for crisis-adjacent matches (self-harm).</summary>
    string CrisisChatReply { get; }

    /// <summary>Validation error text for APIs that throw (garden, diagnosis).</summary>
    string BlockedApiMessage { get; }
}
