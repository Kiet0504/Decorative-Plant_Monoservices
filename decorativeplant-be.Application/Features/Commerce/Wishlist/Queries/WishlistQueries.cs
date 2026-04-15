using MediatR;
using decorativeplant_be.Application.Common.DTOs.Commerce;

namespace decorativeplant_be.Application.Features.Commerce.Wishlist.Queries;

public class GetWishlistQuery : IRequest<List<ProductListingResponse>>
{
    public Guid UserId { get; set; }
}

