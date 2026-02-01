namespace decorativeplant_be.Domain.Entities;

public class PlantCategory : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public Guid? ParentId { get; set; }
    public PlantCategory? Parent { get; set; }
    
    public string? IconUrl { get; set; }
}
