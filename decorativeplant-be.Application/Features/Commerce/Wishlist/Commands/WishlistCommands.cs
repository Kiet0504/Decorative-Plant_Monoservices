using MediatR;
using decorativeplant_be.Application.Common.DTOs.Commerce;

namespace decorativeplant_be.Application.Features.Commerce.Wishlist.Commands;

public class AddWishlistItemCommand : IRequest<bool>
{
    public Guid UserId { get; set; }
    public AddWishlistItemRequest Request { get; set; } = null!;
}

public class RemoveWishlistItemCommand : IRequest<bool>
{
    public Guid UserId { get; set; }
    public Guid ListingId { get; set; }
}

