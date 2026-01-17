using System.Text.Json;

namespace decorativeplant_be.Domain.Entities;

public class ShoppingCart : BaseEntity
{
    public Guid UserId { get; set; }
    public JsonDocument? ItemsJson { get; set; } // Array: [{listing_id, qty, unit_price_snapshot}]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public UserAccount UserAccount { get; set; } = null!;
}
