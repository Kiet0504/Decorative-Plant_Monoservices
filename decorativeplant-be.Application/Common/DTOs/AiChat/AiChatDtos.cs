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
