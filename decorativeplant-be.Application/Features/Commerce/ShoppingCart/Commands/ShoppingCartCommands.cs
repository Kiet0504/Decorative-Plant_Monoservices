using MediatR;
using decorativeplant_be.Application.Common.DTOs.Commerce;

namespace decorativeplant_be.Application.Features.Commerce.ShoppingCart.Commands;

public class AddToCartCommand : IRequest<ShoppingCartResponse>
{
    public Guid UserId { get; set; }
    public AddToCartRequest Request { get; set; } = null!;
}

public class UpdateCartItemCommand : IRequest<ShoppingCartResponse>
{
    public Guid UserId { get; set; }
    public Guid ListingId { get; set; }
    public UpdateCartItemRequest Request { get; set; } = null!;
}

public class RemoveCartItemCommand : IRequest<ShoppingCartResponse>
{
    public Guid UserId { get; set; }
    public Guid ListingId { get; set; }
}

public class ClearCartCommand : IRequest<bool>
{
    public Guid UserId { get; set; }
}
