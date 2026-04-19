namespace decorativeplant_be.Application.Common.Interfaces;

/// <summary>
/// Ensures chat stays on Decorative Plant topics: plants, shop, My Garden, room scan, disease/care — not general chat.
/// </summary>
public interface IPlantAssistantScopeService
{
    /// <summary>
    /// When false, the caller should return <see cref="OutOfScopeReply"/> without calling the conversational model.
    /// </summary>
    bool IsInScopeForChat(
        string combinedUserMessages,
        bool hasAttachedImage,
        bool hasRoomScanFollowUp,
        bool hasGardenPlantFocus,
        bool hasArPreviewContext = false,
        bool hasProductListingContext = false);

    /// <summary>
    /// Stricter check for a single user-supplied string (diagnosis caption, care log note) without context bypasses.
    /// </summary>
    bool IsInScopeForPlainUserText(string? text);

    string OutOfScopeReply { get; }

    /// <summary>For <see cref="ValidationException"/> on non-chat APIs.</summary>
    string OutOfScopeApiMessage { get; }
}
