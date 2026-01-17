using System.Text.Json;

namespace decorativeplant_be.Domain.Entities;

public class ProductReview : BaseEntity
{
    public Guid ListingId { get; set; }
    public Guid UserId { get; set; }
    public Guid OrderId { get; set; }
    public int Rating { get; set; } // 1-5
    public string? Comment { get; set; }
    public JsonDocument? ImagesJson { get; set; }

    // Navigation properties
    public Listing Listing { get; set; } = null!;
    public UserAccount UserAccount { get; set; } = null!;
    public OrderHeader OrderHeader { get; set; } = null!;
}
