namespace decorativeplant_be.Application.Common.DTOs.Garden;

/// <summary>
/// Response DTO for a garden plant.
/// </summary>
public class GardenPlantDto
{
    public Guid Id { get; set; }

    public Guid? UserId { get; set; }

    public Guid? TaxonomyId { get; set; }

    public GardenPlantDetailsDto? Details { get; set; }

    public string? ImageUrl { get; set; }

    public bool IsArchived { get; set; }

    public DateTime? CreatedAt { get; set; }

    /// <summary>Optional taxonomy summary when included.</summary>
    public TaxonomySummaryDto? Taxonomy { get; set; }
}

/// <summary>
/// Summary of plant taxonomy for display.
/// </summary>
public class TaxonomySummaryDto
{
    public Guid Id { get; set; }

    public string ScientificName { get; set; } = string.Empty;

    public string? CommonName { get; set; }
}

/// <summary>
/// Preview of taxonomy resolved from a purchase order line (same logic as import-from-purchase).
/// </summary>
public class OrderItemTaxonomyPreviewDto
{
    public bool Resolved { get; set; }

    public Guid? TaxonomyId { get; set; }

    public string? ScientificName { get; set; }

    public string? CommonName { get; set; }
}
