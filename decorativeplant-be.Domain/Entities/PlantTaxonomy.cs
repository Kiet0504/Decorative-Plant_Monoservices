using System.Text.Json;

namespace decorativeplant_be.Domain.Entities;

/// <summary>
/// Master plant species data. JSONB: common_names, taxonomy_info, care_info, growth_info.
/// See docs/JSONB_SCHEMA_REFERENCE.md
/// </summary>
public class PlantTaxonomy : BaseEntity
{
    public string ScientificName { get; set; } = string.Empty;
    public JsonDocument? CommonNames { get; set; }
    public JsonDocument? TaxonomyInfo { get; set; }
    public JsonDocument? CareInfo { get; set; }
    public JsonDocument? GrowthInfo { get; set; }
    public string? ImageUrl { get; set; }
    public JsonDocument? Images { get; set; }
    public Guid? CategoryId { get; set; }

    public PlantCategory? Category { get; set; }
    public ICollection<PlantBatch> PlantBatches { get; set; } = new List<PlantBatch>();
    public ICollection<GardenPlant> GardenPlants { get; set; } = new List<GardenPlant>();
}
