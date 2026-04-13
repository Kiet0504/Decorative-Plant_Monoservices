namespace decorativeplant_be.Application.Common.DTOs.Garden;

public sealed class AiCareAdviceDto
{
    public string Summary { get; set; } = string.Empty;
    public List<string> Do { get; set; } = new();
    public List<string> Dont { get; set; } = new();
    public List<string> RiskNotes { get; set; } = new();
    /// <summary>low|medium|high</summary>
    public string Confidence { get; set; } = "medium";

    /// <summary>
    /// Optional AI-generated enrichment when taxonomy data is missing.
    /// </summary>
    public AiPlantEnrichmentDto? Enrichment { get; set; }

    // Metadata (useful for caching/debug)
    public string? Model { get; set; }
    public DateTime? GeneratedAtUtc { get; set; }
}

public sealed class AiPlantEnrichmentDto
{
    public string? Description { get; set; }
    public string? Origin { get; set; }
    public string? GrowthRate { get; set; }
    public AiCareRequirementsDto? CareRequirements { get; set; }
}

public sealed class AiCareRequirementsDto
{
    public string? Lighting { get; set; }
    public string? Temperature { get; set; }
    public string? Humidity { get; set; }
}

