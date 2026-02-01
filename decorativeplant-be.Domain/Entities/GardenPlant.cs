using System.Text.Json.Nodes;

namespace decorativeplant_be.Domain.Entities;

public class GardenPlant : BaseEntity
{
    public Guid UserId { get; set; }
    public UserAccount User { get; set; } = null!;
    
    public Guid? TaxonomyId { get; set; }
    public PlantTaxonomy? Taxonomy { get; set; }
    
    public JsonNode? Details { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsArchived { get; set; }
}
