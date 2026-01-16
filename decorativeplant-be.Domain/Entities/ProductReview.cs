using System.Text.Json.Nodes;

namespace decorativeplant_be.Domain.Entities;

public class ProductReview : BaseEntity
{
    public Guid ListingId { get; set; }
    public Listing Listing { get; set; } = null!;
    
    public Guid UserId { get; set; }
    public UserAccount User { get; set; } = null!;
    
    // Note: OrderId in ERD is present in product_review table
    public Guid OrderId { get; set; }
    public OrderHeader Order { get; set; } = null!;
    
    public int Rating { get; set; }
    public string Comment { get; set; } = string.Empty;
    public JsonNode? ImagesJson { get; set; }
}
