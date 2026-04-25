using decorativeplant_be.Application.Common.DTOs.RoomScan;
using decorativeplant_be.Application.Common.DTOs.Garden;

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

    /// <summary>Whether to include user profile (light, humidity, goals, style, etc.) in the system prompt.</summary>
    public bool IncludeUserProfileContext { get; set; } = true;

    /// <summary>Whether to include the user's recent garden plants list in the system prompt.</summary>
    public bool IncludeGardenListContext { get; set; } = true;

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

    /// <summary>
    /// Listing ids the client has already rendered as recommendations in this chat.
    /// Used to reduce repeats for profile-based shop recommendations.
    /// </summary>
    public List<Guid>? PreviousRecommendationListingIds { get; set; }

    /// <summary>Optional AR preview session from <c>POST /v1/ar-preview/sessions</c> (placement + scan JSON on server).</summary>
    public Guid? ArSessionId { get; set; }

    /// <summary>Optional shop listing when discussing a product placed in AR (may duplicate session payload).</summary>
    public Guid? ProductListingId { get; set; }

    /// <summary>Optional client-only placement hints (JSON string) merged into prompts when session is missing fields.</summary>
    public string? PlacementContextJson { get; set; }

    /// <summary>
    /// Minutes to add to UTC to get the user's local time (same as <c>-new Date().getTimezoneOffset()</c> in JavaScript).
    /// Used for snapping suggested schedules to morning/afternoon/evening in local time.
    /// </summary>
    public int? UtcOffsetMinutes { get; set; }
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

    /// <summary>
    /// Optional care schedule suggestions for the focused garden plant.
    /// Returned when the server believes a structured follow-up plan would help (e.g. after photo diagnosis).
    /// Client may offer an "Accept" action to save these into the care calendar.
    /// </summary>
    public List<CareScheduleTaskInfoDto>? SuggestedSchedules { get; set; }

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

    /// <summary>
    /// Optional structured UI suggestions for the client (setup idea cards, quick actions, etc.).
    /// Persisted into chat history metadata when using the persisted chat endpoints.
    /// </summary>
    public AiChatUiSuggestionsDto? UiSuggestions { get; set; }
}

public sealed class AiChatUiSuggestionsDto
{
    public List<AiChatSetupIdeaCardDto> SetupIdeaCards { get; set; } = new();
    public List<AiChatQuickActionDto> QuickActions { get; set; } = new();
    public List<AiChatDesignStyleOptionDto> AvailableStyles { get; set; } = new();
    public List<AiChatContextInferenceDto> ContextInferences { get; set; } = new();
}

public sealed class AiChatDesignStyleOptionDto
{
    public string StyleKey { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

public sealed class AiChatContextInferenceDto
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public string? Evidence { get; set; }
    public AiChatContextPatchEnvelopeDto? ContextPatch { get; set; }
}

public sealed class AiChatSetupIdeaCardDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;

    public string StyleKey { get; set; } = string.Empty;
    public int PlantCount { get; set; }
    public string Difficulty { get; set; } = "beginner";

    /// <summary>PATCH_ONLY | PATCH_AND_PREFILL | PATCH_AND_SEND</summary>
    public string ActionMode { get; set; } = "PATCH_AND_PREFILL";

    public AiChatContextPatchEnvelopeDto? ContextPatch { get; set; }

    /// <summary>When set, the client can lazily request image generation for a preview.</summary>
    public string? PreviewImagePrompt { get; set; }

    /// <summary>Optional already-generated preview image url.</summary>
    public string? PreviewImageUrl { get; set; }
}

public sealed class AiChatQuickActionDto
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string ActionType { get; set; } = string.Empty;
    public AiChatContextPatchEnvelopeDto? ContextPatch { get; set; }
}

public sealed class AiChatContextPatchEnvelopeDto
{
    public int Version { get; set; } = 1;
    public AiChatContextPatchDto Patch { get; set; } = new();
}

public sealed class AiChatContextPatchDto
{
    public AiChatPlantContextPatchDto? PlantContext { get; set; }
    public AiChatDesignContextPatchDto? DesignContext { get; set; }
    public AiChatRoomContextPatchDto? RoomContext { get; set; }
}

public sealed class AiChatPlantContextPatchDto
{
    public bool? IncludeUserProfileContext { get; set; }
    public bool? IncludeGardenListContext { get; set; }
    public bool? IncludeFocusPlantContext { get; set; }
    public Guid? GardenPlantId { get; set; }
}

public sealed class AiChatDesignContextPatchDto
{
    public string? StyleKey { get; set; }
    public string? GoalKey { get; set; }
    public string? PlacementKey { get; set; }
}

public sealed class AiChatRoomContextPatchDto
{
    /// <summary>low | medium | bright</summary>
    public string? LightKey { get; set; }
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
