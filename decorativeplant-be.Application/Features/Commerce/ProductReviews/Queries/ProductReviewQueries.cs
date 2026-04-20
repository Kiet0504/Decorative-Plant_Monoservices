using MediatR;
using decorativeplant_be.Application.Common.DTOs.Commerce;

using decorativeplant_be.Application.Common.DTOs.Common;

namespace decorativeplant_be.Application.Features.Commerce.ProductReviews.Queries;

public class GetReviewsByListingQuery : IRequest<PagedResult<ProductReviewResponse>> 
{ 
    public Guid ListingId { get; set; } 
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
public class GetReviewByIdQuery : IRequest<ProductReviewResponse?> { public Guid Id { get; set; } }
public class GetAllReviewsQuery : IRequest<PagedResult<ProductReviewResponse>>
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
