namespace decorativeplant_be.Application.Common.DTOs.Commerce;

public class CartItemDto
{
    public Guid ListingId { get; set; }
    public int Quantity { get; set; }
    public DateTime? AddedAt { get; set; }
}

public class AddToCartRequest
{
    public Guid ListingId { get; set; }
    public int Quantity { get; set; } = 1;
}

public class UpdateCartItemRequest
{
    public int Quantity { get; set; }
}

public class ShoppingCartResponse
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public List<CartItemDto> Items { get; set; } = new();
    public DateTime? UpdatedAt { get; set; }
}
