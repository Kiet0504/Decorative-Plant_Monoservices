using MediatR;

namespace decorativeplant_be.Application.Features.PlantLibrary.Commands;

public class DeletePlantTaxonomyCommand : IRequest<Unit>
{
    public Guid Id { get; set; }
}
