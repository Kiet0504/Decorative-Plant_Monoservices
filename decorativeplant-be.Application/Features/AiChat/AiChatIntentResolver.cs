namespace decorativeplant_be.Application.Features.AiChat;

/// <summary>
/// Single place that decides the main handling mode for a chat turn (after safety/scope checks).
/// Order: room-scan thread wins over profile shop; profile shop is only when the thread is not already room-scan based.
/// </summary>
public enum AiChatTurnIntent
{
    /// <summary>Normal assistant Q&amp;A; optional disease-help tone only.</summary>
    Conversational = 0,

    /// <summary>Thread contains room-scan catalog context or a room-scan follow-up payload — shop answers must stay in that flow.</summary>
    RoomScanThread = 1,

    /// <summary>User asked for shop/catalog picks from saved profile (no room photo in thread).</summary>
    ProfileShopRecommendations = 2
}

/// <summary>
/// Maps <see cref="AiChatTurnIntent"/> to the API string sent to clients as <c>resolvedIntent</c>.
/// Main routing (room-scan vs profile shop vs conversational) is done in <c>SendAiChatMessageCommandHandler</c>
/// using <see cref="decorativeplant_be.Application.Common.Interfaces.IAiChatProfileShopIntentDetector"/> for non-room-scan turns.
/// </summary>
public static class AiChatIntentResolver
{
    public const string ResolvedConversational = "conversational";
    public const string ResolvedRoomScanThread = "room_scan_thread";
    public const string ResolvedProfileShop = "profile_shop";
    public const string ResolvedFormalDiagnosis = "formal_diagnosis";

    /// <summary>Room-scan context always wins — no profile-shop ranking from synthetic profile alone.</summary>
    public static bool IsRoomScanThread(
        bool conversationIncludesRoomScanCatalog,
        bool hasRoomScanFollowUpPayload) =>
        hasRoomScanFollowUpPayload || conversationIncludesRoomScanCatalog;

    public static string ToApiValue(AiChatTurnIntent intent) =>
        intent switch
        {
            AiChatTurnIntent.RoomScanThread => ResolvedRoomScanThread,
            AiChatTurnIntent.ProfileShopRecommendations => ResolvedProfileShop,
            _ => ResolvedConversational
        };
}
