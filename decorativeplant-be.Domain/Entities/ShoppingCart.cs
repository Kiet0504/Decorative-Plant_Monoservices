using System.Text.Json;

namespace decorativeplant_be.Domain.Entities;

/// <summary>
/// Shopping cart. items JSONB = merged cart_item. See JSONB_SCHEMA_REFERENCE.md
/// </summary>
public class ShoppingCart
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public JsonDocument? Items { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public UserAccount? User { get; set; }
}
