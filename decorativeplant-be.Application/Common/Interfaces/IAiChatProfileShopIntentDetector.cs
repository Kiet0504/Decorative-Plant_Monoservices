namespace decorativeplant_be.Application.Common.Interfaces;

/// <summary>
/// When the chat thread is <b>not</b> a room-scan thread, decides whether to run profile-based
/// shop/catalog ranking (<c>profile_shop</c> intent). Uses keyword heuristics first, then a small
/// JSON LLM call when <c>Ollama:IntentClassificationModel</c> (or main model) is configured.
/// </summary>
public interface IAiChatProfileShopIntentDetector
{
    /// <summary>
    /// Returns true if this turn should load ranked product listings for the assistant reply.
    /// </summary>
    Task<bool> WantsProfileShopCatalogAsync(
        string? latestUserMessage,
        CancellationToken cancellationToken = default);
}
