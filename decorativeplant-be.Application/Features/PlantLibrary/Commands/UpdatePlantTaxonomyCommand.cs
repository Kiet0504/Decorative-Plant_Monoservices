using decorativeplant_be.Application.Features.PlantLibrary.DTOs;
using MediatR;

namespace decorativeplant_be.Application.Features.PlantLibrary.Commands;

public class UpdatePlantTaxonomyCommand : IRequest<PlantTaxonomyDto>
{
    public Guid Id { get; set; }
    public string ScientificName { get; set; } = string.Empty;
    public string? CommonNameEn { get; set; }
    public string? CommonNameVi { get; set; }
    
    public Dictionary<string, object>? TaxonomyInfo { get; set; }
    public Dictionary<string, object>? CareInfo { get; set; }
    public Dictionary<string, object>? GrowthInfo { get; set; }
    
    public string? ImageUrl { get; set; }
    public Guid? CategoryId { get; set; }
}
