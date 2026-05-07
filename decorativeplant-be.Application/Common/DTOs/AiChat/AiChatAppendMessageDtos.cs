using decorativeplant_be.Application.Common.DTOs.RoomScan;

namespace decorativeplant_be.Application.Common.DTOs.AiChat;

public sealed class AiChatAppendMessageRequestDto
{
    public Guid ThreadId { get; set; }
    public string Role { get; set; } = "assistant";
    public string Content { get; set; } = string.Empty;

    /// <summary>Existing media URL previously generated/uploaded by other endpoints.</summary>
    public string? AttachmentUrl { get; set; }
    public string? AttachmentMimeType { get; set; }

    public List<RoomScanRecommendationDto>? NewRecommendations { get; set; }
}

public sealed class AiChatAppendMessageResultDto
{
    public AiChatHistoryMessageDto SavedMessage { get; set; } = new();
}

