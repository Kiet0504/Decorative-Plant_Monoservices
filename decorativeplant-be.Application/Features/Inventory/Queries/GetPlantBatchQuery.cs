using decorativeplant_be.Application.Features.Inventory.DTOs;
using MediatR;

namespace decorativeplant_be.Application.Features.Inventory.Queries;

public class GetPlantBatchQuery : IRequest<PlantBatchDto>
{
    public Guid Id { get; set; }
}
