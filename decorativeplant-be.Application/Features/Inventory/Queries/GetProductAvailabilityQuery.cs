using decorativeplant_be.Application.Features.Inventory.DTOs;
using MediatR;

namespace decorativeplant_be.Application.Features.Inventory.Queries;

public class GetProductAvailabilityQuery : IRequest<ProductAvailabilityDto>
{
    public Guid ProductListingId { get; set; }
}
