using System.Text.Json.Nodes;

namespace decorativeplant_be.Domain.Entities;

public class ShoppingCart : BaseEntity
{
    public Guid UserId { get; set; }
    public UserAccount User { get; set; } = null!;
    
    public JsonNode? Items { get; set; } // [{"listing_id": "...", "quantity": 1}]
}
