using decorativeplant_be.Application.Features.Inventory.DTOs;
using MediatR;

namespace decorativeplant_be.Application.Features.Inventory.Commands;

public class UpdatePlantBatchCommand : IRequest<PlantBatchDto>
{
    public Guid Id { get; set; }
    public Dictionary<string, object>? SourceInfo { get; set; }
    public Dictionary<string, object>? Specs { get; set; }
}
