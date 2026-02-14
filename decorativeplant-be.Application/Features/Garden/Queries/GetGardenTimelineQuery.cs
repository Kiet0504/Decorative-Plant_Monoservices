using decorativeplant_be.Application.Common.DTOs.Garden;
using MediatR;

namespace decorativeplant_be.Application.Features.Garden.Queries;

/// <summary>
/// Query to get merged timeline (care logs, milestones, diagnoses) for a garden plant.
/// </summary>
public class GetGardenTimelineQuery : IRequest<List<TimelineItemDto>>
{
    public Guid UserId { get; set; }

    public Guid PlantId { get; set; }

    public DateTime? From { get; set; }

    public DateTime? To { get; set; }

    public int Limit { get; set; } = 50;
}
