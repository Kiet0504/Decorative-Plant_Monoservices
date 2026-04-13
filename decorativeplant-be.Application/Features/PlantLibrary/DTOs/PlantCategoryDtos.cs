namespace decorativeplant_be.Application.Features.PlantLibrary.DTOs;

public class PlantCategoryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public Guid? ParentId { get; set; }
    public string? IconUrl { get; set; }
}
