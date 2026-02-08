using System.Text.Json;

namespace decorativeplant_be.Domain.Entities;

/// <summary>
/// User notification. JSONB: data — metadata (order_id, plant_id, etc.). See JSONB_SCHEMA_REFERENCE.md
/// </summary>
public class Notification : BaseEntity
{
    public Guid UserId { get; set; }
    public string? Type { get; set; }
    public string? Title { get; set; }
    public string? Body { get; set; }
    public JsonDocument? Data { get; set; }
    public bool IsRead { get; set; } = false;

    public UserAccount User { get; set; } = null!;
}
