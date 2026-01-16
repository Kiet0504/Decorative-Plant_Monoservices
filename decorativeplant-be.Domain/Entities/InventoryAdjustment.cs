namespace decorativeplant_be.Domain.Entities;

public class InventoryAdjustment : BaseEntity
{
    public Guid StockId { get; set; }
    public BatchStock Stock { get; set; } = null!;
    
    public string Reason { get; set; } = string.Empty;
    public int QuantityChange { get; set; }
    public string Note { get; set; } = string.Empty;
}
