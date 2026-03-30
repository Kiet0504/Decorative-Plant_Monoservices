using decorativeplant_be.Application.Common.DTOs.Recommendations;
using MediatR;

namespace decorativeplant_be.Application.Features.Recommendations.Queries;

public class GetProductRecommendationsQuery : IRequest<ProductRecommendationsResponse>
{
    public Guid UserId { get; set; }

    public ProductRecommendationsRequest Request { get; set; } = new();
}

