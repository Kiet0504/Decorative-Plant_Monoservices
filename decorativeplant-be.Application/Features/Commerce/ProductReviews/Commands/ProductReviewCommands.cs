using MediatR;
using decorativeplant_be.Application.Common.DTOs.Commerce;

namespace decorativeplant_be.Application.Features.Commerce.ProductReviews.Commands;

public class CreateProductReviewCommand : IRequest<ProductReviewResponse> { public Guid UserId { get; set; } public CreateProductReviewRequest Request { get; set; } = null!; }
public class UpdateReviewStatusCommand : IRequest<ProductReviewResponse> { public Guid Id { get; set; } public UpdateReviewStatusRequest Request { get; set; } = null!; }
public class DeleteProductReviewCommand : IRequest<bool> { public Guid Id { get; set; } }
