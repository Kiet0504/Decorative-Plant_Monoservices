using decorativeplant_be.Application.Common.DTOs.Garden;
using MediatR;

namespace decorativeplant_be.Application.Features.Garden.Commands;

/// <summary>
/// Command to update only the health status of a garden plant.
/// </summary>
public class UpdatePlantHealthCommand : IRequest<GardenPlantDto>
{
    public Guid UserId { get; set; }

    public Guid Id { get; set; }

    /// <summary>healthy|needs_attention|struggling</summary>
    public string Health { get; set; } = string.Empty;
}
