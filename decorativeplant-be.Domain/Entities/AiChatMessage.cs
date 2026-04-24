using System.Text.Json;

namespace decorativeplant_be.Domain.Entities;

public sealed class AiChatMessage
{
    public Guid Id { get; set; }
    public Guid ThreadId { get; set; }

    /// <summary>user or assistant</summary>
    public string Role { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public string? AttachmentUrl { get; set; }
    public string? AttachmentMimeType { get; set; }

    /// <summary>Diagnosis summary payload (jsonb) when available.</summary>
    public JsonDocument? DiagnosisJson { get; set; }

    /// <summary>Room scan / catalog picks payload (jsonb) when available.</summary>
    public JsonDocument? RecommendationsJson { get; set; }

    /// <summary>Misc per-message metadata (jsonb): resolved intent, flags, context snapshot, etc.</summary>
    public JsonDocument? MetadataJson { get; set; }

    public AiChatThread? Thread { get; set; }
}

