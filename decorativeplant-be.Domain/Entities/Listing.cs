using System.Text.Json;

namespace decorativeplant_be.Domain.Entities;

public class Listing : BaseEntity
{
    public Guid StoreId { get; set; }
    public Guid StockId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string Currency { get; set; } = "VND";
    public string Status { get; set; } = string.Empty; // Active/SoldOut/Hidden
    public JsonDocument? PhotosJson { get; set; } // Array of image URLs
    public int MinOrderQty { get; set; } = 1;
    public int? MaxOrderQty { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Store Store { get; set; } = null!;
    public BatchStock BatchStock { get; set; } = null!;
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    public ICollection<ProductReview> ProductReviews { get; set; } = new List<ProductReview>();
}
