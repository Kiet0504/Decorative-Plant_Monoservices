using System.Text.Json.Nodes;

namespace decorativeplant_be.Domain.Entities;

public class PlantTaxonomy : BaseEntity
{
    public string ScientificName { get; set; } = string.Empty;
    
    // JSONB fields
    public JsonNode? CommonNames { get; set; } // {"en": "...", "vi": "..."}
    public JsonNode? TaxonomyInfo { get; set; } // family, genus, species, cultivar
    public JsonNode? CareInfo { get; set; } // care_level, light, water...
    public JsonNode? GrowthInfo { get; set; } // growth_rate, max_height...
    
    public string? ImageUrl { get; set; }
    
    public Guid? CategoryId { get; set; }
    public PlantCategory? Category { get; set; }
}
