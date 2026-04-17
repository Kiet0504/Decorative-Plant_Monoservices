using decorativeplant_be.Application.Features.PlantLibrary.DTOs;
using MediatR;

namespace decorativeplant_be.Application.Features.PlantLibrary.Commands;

public class CreatePlantTaxonomyCommand : IRequest<PlantTaxonomyDto>
{
    public string ScientificName { get; set; } = string.Empty;
    public string? CommonNameEn { get; set; }
    public string? CommonNameVi { get; set; }
    
    public Dictionary<string, object>? TaxonomyInfo { get; set; }
    public Dictionary<string, object>? CareInfo { get; set; }
    public Dictionary<string, object>? GrowthInfo { get; set; }
    
    public string? ImageUrl { get; set; }
    public decimal? DefaultPrice { get; set; }
    public Guid? CategoryId { get; set; }
    public string? CategoryName { get; set; }
}
