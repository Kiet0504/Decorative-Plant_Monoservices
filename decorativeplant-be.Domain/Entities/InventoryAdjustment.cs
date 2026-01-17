namespace decorativeplant_be.Domain.Entities;

public class InventoryAdjustment : BaseEntity
{
    public Guid StockId { get; set; }
    public string Reason { get; set; } = string.Empty; // Damage/Lost/Audit
    public int QuantityChange { get; set; }
    public string? Note { get; set; }

    // Navigation properties
    public BatchStock BatchStock { get; set; } = null!;
}
