using decorativeplant_be.Application.Common.DTOs.AiChat;
using MediatR;

namespace decorativeplant_be.Application.Features.AiChat.Commands;

public sealed class SendAiChatMessageCommand : IRequest<AiChatReplyDto>
{
    public Guid UserId { get; set; }
    public List<AiChatMessageDto> Messages { get; set; } = new();

    public bool IncludeUserProfileContext { get; set; } = true;
    public bool IncludeGardenListContext { get; set; } = true;

    public Guid? GardenPlantId { get; set; }

    public string? AttachedImageBase64 { get; set; }
    public string? AttachedImageMimeType { get; set; }

    public RoomScanChatFollowUpDto? RoomScanFollowUp { get; set; }

    /// <summary>
    /// Listing ids the client has already shown as "Suggested from our shop" in this chat.
    /// Used to reduce repeats for profile-based shop recommendations (not room-scan follow-ups).
    /// </summary>
    public List<Guid>? PreviousRecommendationListingIds { get; set; }

    public Guid? ArSessionId { get; set; }

    public Guid? ProductListingId { get; set; }

    public string? PlacementContextJson { get; set; }

    public int? UtcOffsetMinutes { get; set; }
}
