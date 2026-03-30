using decorativeplant_be.Application.Common.DTOs.Recommendations;
using decorativeplant_be.Application.Services.Recommendations;

namespace decorativeplant_be.Tests;

public class RecommendationDtosTests
{
    [Fact]
    public void ProductRecommendationsRequest_DefaultLimit_Is5()
    {
        var req = new ProductRecommendationsRequest();
        Assert.Equal(5, req.Limit);
    }

    [Fact]
    public void RecommendationEngine_Interface_Exists()
    {
        // Compile-time assertion that interface is accessible from tests project.
        Assert.NotNull(typeof(IRecommendationEngine));
    }
}
