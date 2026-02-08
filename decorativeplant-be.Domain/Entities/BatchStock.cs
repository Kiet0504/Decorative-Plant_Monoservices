using System.Text.Json;

namespace decorativeplant_be.Domain.Entities;

/// <summary>
/// Stock of a batch at a location. JSONB: quantities, last_count_info. See JSONB_SCHEMA_REFERENCE.md
/// </summary>
public class BatchStock
{
    public Guid Id { get; set; }
    public Guid? BatchId { get; set; }
    public Guid? LocationId { get; set; }
    public JsonDocument? Quantities { get; set; }
    public string? HealthStatus { get; set; }
    public JsonDocument? LastCountInfo { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public PlantBatch? Batch { get; set; }
    public InventoryLocation? Location { get; set; }
    public ICollection<StockAdjustment> StockAdjustments { get; set; } = new List<StockAdjustment>();
    public ICollection<HealthIncident> HealthIncidents { get; set; } = new List<HealthIncident>();
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}
