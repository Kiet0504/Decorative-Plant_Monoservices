using decorativeplant_be.Application.Common.DTOs.RoomScan;

namespace decorativeplant_be.Application.Common.DTOs.AiChat;

public sealed class AiChatHistoryMessageDto
{
    public Guid Id { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public string? AttachmentUrl { get; set; }
    public string? AttachmentMimeType { get; set; }

    public AiChatDiagnosisSummaryDto? Diagnosis { get; set; }
    /// <summary>When set, diagnosis was run with focus on this garden plant (show My Garden actions).</summary>
    public Guid? DiagnosisGardenPlantId { get; set; }

    public List<RoomScanRecommendationDto>? NewRecommendations { get; set; }

    public AiChatUiSuggestionsDto? UiSuggestions { get; set; }
}

public sealed class AiChatHistoryDto
{
    public Guid? ThreadId { get; set; }
    public List<AiChatHistoryMessageDto> Messages { get; set; } = new();
}

public sealed class AiChatThreadListItemDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
}

public sealed class AiChatThreadListDto
{
    public List<AiChatThreadListItemDto> Threads { get; set; } = new();
}

public sealed class AiChatCreateThreadRequestDto
{
    public string? Title { get; set; }
}

public sealed class AiChatCreateThreadResultDto
{
    public AiChatThreadListItemDto Thread { get; set; } = new();
}

public sealed class AiChatRenameThreadRequestDto
{
    public string Title { get; set; } = string.Empty;
}

public sealed class AiChatSendMessageRequestDto
{
    public string Content { get; set; } = string.Empty;

    public Guid? ThreadId { get; set; }

    public bool IncludeUserProfileContext { get; set; } = true;
    public bool IncludeGardenListContext { get; set; } = true;

    public Guid? GardenPlantId { get; set; }
    public RoomScanChatFollowUpDto? RoomScanFollowUp { get; set; }

    public Guid? ArSessionId { get; set; }
    public Guid? ProductListingId { get; set; }
    public string? PlacementContextJson { get; set; }
    public int? UtcOffsetMinutes { get; set; }

    public string? AttachedImageBase64 { get; set; }
    public string? AttachedImageMimeType { get; set; }

    public string? ClientMessageId { get; set; }
}

public sealed class AiChatSendMessageResultDto
{
    public AiChatReplyDto Reply { get; set; } = new();

    public AiChatHistoryMessageDto SavedUserMessage { get; set; } = new();
    public AiChatHistoryMessageDto SavedAssistantMessage { get; set; } = new();
}

