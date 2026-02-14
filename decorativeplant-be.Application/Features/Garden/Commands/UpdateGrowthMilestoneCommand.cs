using decorativeplant_be.Application.Common.DTOs.Garden;
using MediatR;

namespace decorativeplant_be.Application.Features.Garden.Commands;

/// <summary>
/// Command to update a growth milestone.
/// </summary>
public class UpdateGrowthMilestoneCommand : IRequest<GrowthMilestoneDto>
{
    public Guid UserId { get; set; }

    public Guid PlantId { get; set; }

    public Guid MilestoneId { get; set; }

    public string? Type { get; set; }

    public DateTime? OccurredAt { get; set; }

    public string? Notes { get; set; }

    public string? ImageUrl { get; set; }
}
