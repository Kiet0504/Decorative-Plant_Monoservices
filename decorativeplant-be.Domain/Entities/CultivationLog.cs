using System.Text.Json;

namespace decorativeplant_be.Domain.Entities;

/// <summary>
/// Cultivation activity log. JSONB: details. See JSONB_SCHEMA_REFERENCE.md
/// </summary>
public class CultivationLog
{
    public Guid Id { get; set; }
    public Guid? BatchId { get; set; }
    public Guid? LocationId { get; set; }
    public string? ActivityType { get; set; }
    public string? Description { get; set; }
    public JsonDocument? Details { get; set; }
    public Guid? PerformedBy { get; set; }
    public DateTime? PerformedAt { get; set; }

    public PlantBatch? Batch { get; set; }
    public InventoryLocation? Location { get; set; }
    public UserAccount? PerformedByUser { get; set; }
}
