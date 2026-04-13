using System.Text.Json;

namespace decorativeplant_be.Domain.Entities;

public class ArPreviewSession
{
    public Guid Id { get; set; }

    public Guid? UserId { get; set; }

    /// <summary>
    /// MVP: store scan payload inline as JSONB (planes etc.).
    /// </summary>
    public JsonDocument? ScanJson { get; set; } = JsonDocument.Parse("{}");

    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Random per-session salt used to sign viewer token.
    /// </summary>
    public string TokenSalt { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

