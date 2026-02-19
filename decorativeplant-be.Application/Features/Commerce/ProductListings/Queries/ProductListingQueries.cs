using MediatR;
using decorativeplant_be.Application.Common.DTOs.Commerce;

namespace decorativeplant_be.Application.Features.Commerce.ProductListings.Queries;

public class GetProductListingsQuery : IRequest<List<ProductListingResponse>>
{
    public Guid? BranchId { get; set; }
    public string? Status { get; set; }
    public string? Search { get; set; }
}

public class GetProductListingByIdQuery : IRequest<ProductListingResponse?>
{
    public Guid Id { get; set; }
}
