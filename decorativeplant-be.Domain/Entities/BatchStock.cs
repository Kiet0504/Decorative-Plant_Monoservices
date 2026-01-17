namespace decorativeplant_be.Domain.Entities;

public class BatchStock : BaseEntity
{
    public Guid BatchId { get; set; }
    public Guid LocationId { get; set; }
    public int Quantity { get; set; } // Current quantity
    public int ReservedQuantity { get; set; } // In cart/pending orders
    public string Unit { get; set; } = string.Empty; // pot/plant
    public string? PotSize { get; set; } // C5, C10, 10cm...
    public string? CurrentHealthStatus { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public PlantBatch PlantBatch { get; set; } = null!;
    public InventoryLocation InventoryLocation { get; set; } = null!;
    public ICollection<InventoryAdjustment> InventoryAdjustments { get; set; } = new List<InventoryAdjustment>();
    public ICollection<StorePlantDiagnosis> StorePlantDiagnoses { get; set; } = new List<StorePlantDiagnosis>();
    public ICollection<Listing> Listings { get; set; } = new List<Listing>();
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}
