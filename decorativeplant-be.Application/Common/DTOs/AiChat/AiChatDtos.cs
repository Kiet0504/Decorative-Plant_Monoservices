using decorativeplant_be.Application.Common.DTOs.RoomScan;

namespace decorativeplant_be.Application.Common.DTOs.AiChat;

/// <summary>Single turn in a client-side chat (no system role; server injects personalization).</summary>
public sealed class AiChatMessageDto
{
    /// <summary>user or assistant</summary>
    public string Role { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;
}

public sealed class AiChatRequestDto
{
    public List<AiChatMessageDto> Messages { get; set; } = new();

    /// <summary>Optional plant the user is asking about (must belong to the user).</summary>
    public Guid? GardenPlantId { get; set; }

    /// <summary>Optional image for the latest user turn: raw base64 or data-URL (JPEG/PNG/WebP).</summary>
    public string? AttachedImageBase64 { get; set; }

    /// <summary>MIME type hint, e.g. image/jpeg (optional if data-URL).</summary>
    public string? AttachedImageMimeType { get; set; }

    /// <summary>
    /// When set, the server may re-rank catalog picks for this room profile when chat intent asks for different suggestions.
    /// </summary>
    public RoomScanChatFollowUpDto? RoomScanFollowUp { get; set; }

    /// <summary>Optional AR preview session from <c>POST /v1/ar-preview/sessions</c> (placement + scan JSON on server).</summary>
    public Guid? ArSessionId { get; set; }

    /// <summary>Optional shop listing when discussing a product placed in AR (may duplicate session payload).</summary>
    public Guid? ProductListingId { get; set; }

    /// <summary>Optional client-only placement hints (JSON string) merged into prompts when session is missing fields.</summary>
    public string? PlacementContextJson { get; set; }
}

/// <summary>Context from a completed room scan so /ai/chat can refresh recommendations without re-uploading a photo.</summary>
public sealed class RoomScanChatFollowUpDto
{
    public RoomProfileDto RoomProfile { get; set; } = new();

    public Guid? BranchId { get; set; }
    public decimal? MaxPrice { get; set; }
    public bool PetSafeOnly { get; set; }
    public string? SkillLevel { get; set; }

    /// <summary>Optional override; default is server <c>RoomScan:PipelineMode</c>.</summary>
    public string? PipelineMode { get; set; }

    /// <summary>Earlier suggestion ids to deprioritize or exclude when picking alternatives.</summary>
    public List<Guid>? PreviousListingIds { get; set; }
}

public sealed class AiChatReplyDto
{
    public string Reply { get; set; } = string.Empty;

    /// <summary>
    /// When set to <c>disease_diagnosis</c>, the client may show a CTA for photo-based diagnosis.
    /// </summary>
    public string? SuggestedIntent { get; set; }

    /// <summary>Populated when the formal Gemini + Ollama diagnosis pipeline ran for this message.</summary>
    public AiChatDiagnosisSummaryDto? Diagnosis { get; set; }

    /// <summary>Fresh catalog picks after a room-scan follow-up chat (e.g. user asked for other suggestions).</summary>
    public List<RoomScanRecommendationDto>? NewRecommendations { get; set; }

    /// <summary>
    /// How the server routed this turn: <c>profile_shop</c>, <c>conversational</c>, <c>room_scan_thread</c>, <c>formal_diagnosis</c>.
    /// Clients may use this with <see cref="NewRecommendations"/> to show shop UI.
    /// </summary>
    public string? ResolvedIntent { get; set; }

    /// <summary>True when the message was rejected by server content checks (no LLM call for the main reply).</summary>
    public bool ContentBlocked { get; set; }

    /// <summary>True when the message was off-topic for this app (plants, shop, garden, diagnosis only).</summary>
    public bool OutOfScope { get; set; }
}

public sealed class AiChatDiagnosisSummaryDto
{
    public string Disease { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public List<string> Symptoms { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
    public string? Explanation { get; set; }
}

/// <summary>Internal message shape for Ollama /api/chat.</summary>
public sealed class OllamaChatTurnDto
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;

    /// <summary>Ollama vision: base64 payloads (no data: prefix) for this turn.</summary>
    public List<string>? ImagesBase64 { get; set; }
}
