using MediatR;

namespace decorativeplant_be.Application.Features.Garden.Commands;

/// <summary>
/// Command to remove a growth milestone from a garden plant.
/// </summary>
public class RemoveGrowthMilestoneCommand : IRequest<Unit>
{
    public Guid UserId { get; set; }

    public Guid PlantId { get; set; }

    public Guid MilestoneId { get; set; }
}
