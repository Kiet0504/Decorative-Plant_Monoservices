using decorativeplant_be.Application.Common.DTOs.Recommendations;

namespace decorativeplant_be.Application.Services.Recommendations;

public interface IRecommendationEngine
{
    Task<ProductRecommendationsResponse> RecommendProductsAsync(
        Guid userId,
        ProductRecommendationsRequest request,
        CancellationToken cancellationToken = default);
}

