using System.Text.Json.Nodes;

namespace decorativeplant_be.Domain.Entities;

public class ProductReview : BaseEntity
{
    public Guid? ListingId { get; set; }
    public ProductListing? Listing { get; set; }
    
    public Guid UserId { get; set; }
    public UserAccount User { get; set; } = null!;
    
    public Guid? OrderId { get; set; }
    public OrderHeader? Order { get; set; }
    
    public JsonNode? Content { get; set; }
    public JsonNode? StatusInfo { get; set; }
    public JsonNode? Images { get; set; }
}
