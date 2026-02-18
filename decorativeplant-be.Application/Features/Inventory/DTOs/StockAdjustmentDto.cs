namespace decorativeplant_be.Application.Features.Inventory.DTOs;

public class StockAdjustmentDto
{
    public Guid Id { get; set; }
    public Guid BatchId { get; set; }
    public Guid LocationId { get; set; }
    public int QuantityChange { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // Import, Export, Audit, Loss
    public DateTime AdjustedAt { get; set; }
}
