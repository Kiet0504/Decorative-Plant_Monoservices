using decorativeplant_be.Application.Features.PlantLibrary.DTOs;
using MediatR;

namespace decorativeplant_be.Application.Features.PlantLibrary.Queries;

public class GetPlantTaxonomyQuery : IRequest<PlantTaxonomyDto>
{
    public Guid Id { get; set; }
}
