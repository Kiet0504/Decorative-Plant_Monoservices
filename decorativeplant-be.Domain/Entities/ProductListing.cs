using System.Text.Json;

namespace decorativeplant_be.Domain.Entities;

/// <summary>
/// Product listing (branch + batch). JSONB: product_info, status_info, seo_info, images.
/// See docs/JSONB_SCHEMA_REFERENCE.md
/// </summary>
public class ProductListing
{
    public Guid Id { get; set; }
    public Guid? BranchId { get; set; }
    public Guid? BatchId { get; set; }
    public JsonDocument? ProductInfo { get; set; }
    public JsonDocument? StatusInfo { get; set; }
    public JsonDocument? SeoInfo { get; set; }
    public JsonDocument? Images { get; set; }
    public DateTime? CreatedAt { get; set; }

    public Branch? Branch { get; set; }
    public PlantBatch? Batch { get; set; }
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    public ICollection<ProductReview> ProductReviews { get; set; } = new List<ProductReview>();
}
