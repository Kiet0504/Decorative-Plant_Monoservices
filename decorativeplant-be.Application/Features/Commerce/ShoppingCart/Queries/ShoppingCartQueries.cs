using MediatR;
using decorativeplant_be.Application.Common.DTOs.Commerce;

namespace decorativeplant_be.Application.Features.Commerce.ShoppingCart.Queries;

public class GetCartQuery : IRequest<ShoppingCartResponse>
{
    public Guid UserId { get; set; }
}
