using System.Text.Json.Nodes;

namespace decorativeplant_be.Domain.Entities;

public class Listing : BaseEntity
{
    public Guid StockId { get; set; }
    public BatchStock Stock { get; set; } = null!;
    
    public Guid StoreId { get; set; }
    public Store Store { get; set; } = null!;
    
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Currency { get; set; } = "VND";
    public string Status { get; set; } = string.Empty;
    public JsonNode? PhotosJson { get; set; }
    public int MinOrderQty { get; set; }
    public int MaxOrderQty { get; set; }
}
