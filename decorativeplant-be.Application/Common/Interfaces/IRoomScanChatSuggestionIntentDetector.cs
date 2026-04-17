namespace decorativeplant_be.Application.Common.Interfaces;

/// <summary>
/// Detects when the user wants new catalog picks after a room scan (as opposed to general care chat).
/// Uses an LLM when possible (dedicated intent model or main chat model); heuristics are a fallback.
/// </summary>
public interface IRoomScanChatSuggestionIntentDetector
{
    Task<RoomScanChatSuggestionIntentResult> DetectAsync(
        string? latestUserMessage,
        CancellationToken cancellationToken = default);
}

public sealed class RoomScanChatSuggestionIntentResult
{
    public bool WantsDifferentSuggestions { get; init; }

    /// <summary>Optional constraints distilled for the catalog ranker.</summary>
    public string? RefinementNotes { get; init; }
}
