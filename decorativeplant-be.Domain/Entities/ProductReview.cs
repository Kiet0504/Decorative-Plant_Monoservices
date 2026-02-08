using System.Text.Json;

namespace decorativeplant_be.Domain.Entities;

/// <summary>
/// Product review. JSONB: content, status_info, images. See JSONB_SCHEMA_REFERENCE.md
/// </summary>
public class ProductReview
{
    public Guid Id { get; set; }
    public Guid? ListingId { get; set; }
    public Guid? UserId { get; set; }
    public Guid? OrderId { get; set; }
    public JsonDocument? Content { get; set; }
    public JsonDocument? StatusInfo { get; set; }
    public JsonDocument? Images { get; set; }
    public DateTime? CreatedAt { get; set; }

    public ProductListing? Listing { get; set; }
    public UserAccount? User { get; set; }
    public OrderHeader? Order { get; set; }
}
