namespace decorativeplant_be.Domain.Entities;

public class PlantTaxonomy : BaseEntity
{
    public string ScientificName { get; set; } = string.Empty;
    public string CommonName { get; set; } = string.Empty;
    public string Cultivar { get; set; } = string.Empty;
    public string Family { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
