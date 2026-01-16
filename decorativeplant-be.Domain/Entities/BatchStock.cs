namespace decorativeplant_be.Domain.Entities;

public class BatchStock : BaseEntity
{
    public Guid BatchId { get; set; }
    public PlantBatch Batch { get; set; } = null!;
    
    public Guid LocationId { get; set; }
    public InventoryLocation Location { get; set; } = null!;
    
    public int Quantity { get; set; }
    public int ReservedQuantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string PotSize { get; set; } = string.Empty;
    public string CurrentHealthStatus { get; set; } = string.Empty;
}
