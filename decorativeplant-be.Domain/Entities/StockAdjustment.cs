using System.Text.Json.Nodes;

namespace decorativeplant_be.Domain.Entities;

public class StockAdjustment : BaseEntity
{
    public Guid StockId { get; set; }
    public BatchStock Stock { get; set; } = null!;
    
    public string? Type { get; set; }
    public int QuantityChange { get; set; }
    public string? Reason { get; set; }
    public JsonNode? MetaInfo { get; set; }
}
