using MediatR;
using decorativeplant_be.Application.Common.DTOs.Commerce;

namespace decorativeplant_be.Application.Features.Commerce.ProductReviews.Queries;

public class GetReviewsByListingQuery : IRequest<List<ProductReviewResponse>> { public Guid ListingId { get; set; } }
public class GetReviewByIdQuery : IRequest<ProductReviewResponse?> { public Guid Id { get; set; } }
