namespace decorativeplant_be.Domain.Entities;

/// <summary>
/// Plant category (hierarchical via parent_id).
/// </summary>
public class PlantCategory
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public string? Slug { get; set; }
    public Guid? ParentId { get; set; }
    public string? IconUrl { get; set; }

    public PlantCategory? Parent { get; set; }
    public ICollection<PlantCategory> Children { get; set; } = new List<PlantCategory>();
    public ICollection<PlantTaxonomy> PlantTaxonomies { get; set; } = new List<PlantTaxonomy>();
}
