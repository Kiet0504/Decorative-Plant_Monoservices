using System.Text.Json.Nodes;

namespace decorativeplant_be.Domain.Entities;

public class OrderItem : BaseEntity
{
    public Guid OrderId { get; set; }
    public OrderHeader Order { get; set; } = null!;
    
    public Guid? ListingId { get; set; }
    public ProductListing? Listing { get; set; }
    
    public Guid? StockId { get; set; }
    public BatchStock? Stock { get; set; }
    
    public Guid? BatchId { get; set; }
    public PlantBatch? Batch { get; set; }
    
    public int Quantity { get; set; }
    public JsonNode? Pricing { get; set; }
    public JsonNode? Snapshots { get; set; }
}
