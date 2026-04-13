using MediatR;
using decorativeplant_be.Application.Common.DTOs.Commerce;

using decorativeplant_be.Application.Common.DTOs.Common;

namespace decorativeplant_be.Application.Features.Commerce.ProductListings.Queries;

public class GetProductListingsQuery : IRequest<PagedResult<ProductListingResponse>>
{
    public Guid? BranchId { get; set; }
    public string? Status { get; set; }
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SortBy { get; set; }
    public string? SortOrder { get; set; }
    public bool GroupBySpecies { get; set; } = true;
}

public class GetProductListingByIdQuery : IRequest<ProductListingResponse?>
{
    public Guid Id { get; set; }
}
