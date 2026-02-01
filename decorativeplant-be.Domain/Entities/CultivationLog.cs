using System.Text.Json.Nodes;

namespace decorativeplant_be.Domain.Entities;

public class CultivationLog : BaseEntity
{
    public Guid BatchId { get; set; }
    public PlantBatch Batch { get; set; } = null!;
    
    public Guid LocationId { get; set; }
    public InventoryLocation Location { get; set; } = null!;
    
    public string? ActivityType { get; set; }
    public string? Description { get; set; }
    public JsonNode? Details { get; set; }
    
    public Guid? PerformedBy { get; set; }
    public UserAccount? PerformedByUser { get; set; }
    
    public DateTime? PerformedAt { get; set; }
}
