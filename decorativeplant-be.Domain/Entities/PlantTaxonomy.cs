namespace decorativeplant_be.Domain.Entities;

public class PlantTaxonomy : BaseEntity
{
    public string ScientificName { get; set; } = string.Empty;
    public string CommonName { get; set; } = string.Empty;
    public string? Cultivar { get; set; }
    public string? Family { get; set; }
    public string? Description { get; set; }

    // Navigation properties
    public ICollection<PlantBatch> PlantBatches { get; set; } = new List<PlantBatch>();
    public ICollection<MyGardenPlant> MyGardenPlants { get; set; } = new List<MyGardenPlant>();
}
