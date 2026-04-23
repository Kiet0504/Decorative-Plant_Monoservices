using System.Text.Json.Serialization;

namespace decorativeplant_be.Application.Features.PlantLibrary.DTOs;

public class PlantTaxonomyDto
{
    public Guid Id { get; set; }
    public string ScientificName { get; set; } = string.Empty;
    public string? CommonNameEn { get; set; }
    public string? CommonNameVi { get; set; }
    
    // JSONB Fields mapped to Objects/Dictionaries
    public object? TaxonomyInfo { get; set; }
    public object? CareInfo { get; set; }
    public object? GrowthInfo { get; set; }
    
    public string? ImageUrl { get; set; }
    public Guid? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public object? AutomationMasterData { get; set; }
}

public class PlantTaxonomySummaryDto
{
    public Guid Id { get; set; }
    public string ScientificName { get; set; } = string.Empty;
    public string? CommonName { get; set; } // Legacy combined field
    public string? CommonNameEn { get; set; }
    public string? CommonNameVi { get; set; }
    public object? CareInfo { get; set; }
    public object? GrowthInfo { get; set; }
    public string? ImageUrl { get; set; }
    public string? CategoryName { get; set; }
}

public class CreatePlantTaxonomyDto
{
    public string ScientificName { get; set; } = string.Empty;
    public string? CommonNameEn { get; set; }
    public string? CommonNameVi { get; set; }
    
    public Dictionary<string, object>? TaxonomyInfo { get; set; }
    public Dictionary<string, object>? CareInfo { get; set; }
    public Dictionary<string, object>? GrowthInfo { get; set; }
    
    public string? ImageUrl { get; set; }
    public Guid? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public Dictionary<string, object>? AutomationMasterData { get; set; }
}

public class UpdatePlantTaxonomyDto : CreatePlantTaxonomyDto
{
    // Inherits everything, used for body
}
