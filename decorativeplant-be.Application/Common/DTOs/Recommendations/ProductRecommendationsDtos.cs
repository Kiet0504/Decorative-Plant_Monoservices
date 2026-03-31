namespace decorativeplant_be.Application.Common.DTOs.Recommendations;

public class ProductRecommendationsRequest
{
    public int Limit { get; set; } = 5;

    public Guid? GardenPlantId { get; set; }

    public Guid? BranchId { get; set; }
}

public class ProductRecommendationsResponse
{
    public List<RecommendedProductDto> Items { get; set; } = new();

    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    public string Strategy { get; set; } = "rule";
}

public class RecommendedProductDto
{
    public Guid ListingId { get; set; }

    public double Score { get; set; }

    public List<string> Reasons { get; set; } = new();

    public Guid? TaxonomyId { get; set; }

    public Guid? BatchId { get; set; }

    public string? ImageUrl { get; set; }

    public string? Title { get; set; }

    public string? Price { get; set; }
}

