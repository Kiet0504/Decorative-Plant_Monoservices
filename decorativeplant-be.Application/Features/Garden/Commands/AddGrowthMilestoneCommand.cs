using decorativeplant_be.Application.Common.DTOs.Garden;
using MediatR;

namespace decorativeplant_be.Application.Features.Garden.Commands;

/// <summary>
/// Command to add a growth milestone to a garden plant.
/// </summary>
public class AddGrowthMilestoneCommand : IRequest<GrowthMilestoneDto>
{
    public Guid UserId { get; set; }

    public Guid PlantId { get; set; }

    /// <summary>first_leaf|new_growth|flowering|repotted|other</summary>
    public string Type { get; set; } = string.Empty;

    public DateTime OccurredAt { get; set; }

    public string? Notes { get; set; }

    public string? ImageUrl { get; set; }
}
