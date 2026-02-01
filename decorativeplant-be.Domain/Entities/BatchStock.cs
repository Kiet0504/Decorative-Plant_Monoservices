using System.Text.Json.Nodes;

namespace decorativeplant_be.Domain.Entities;

public class BatchStock : BaseEntity
{
    public Guid BatchId { get; set; }
    public PlantBatch Batch { get; set; } = null!;
    
    public Guid LocationId { get; set; }
    public InventoryLocation Location { get; set; } = null!;
    
    public JsonNode? Quantities { get; set; } // quantity, reserved, available
    public string? HealthStatus { get; set; }
    public JsonNode? LastCountInfo { get; set; }
}
