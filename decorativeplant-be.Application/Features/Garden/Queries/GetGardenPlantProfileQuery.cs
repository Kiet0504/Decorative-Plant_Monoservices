using decorativeplant_be.Application.Common.DTOs.Garden;
using MediatR;

namespace decorativeplant_be.Application.Features.Garden.Queries;

public class GetGardenPlantProfileQuery : IRequest<PlantProfileDto>
{
    public Guid UserId { get; set; }

    public Guid PlantId { get; set; }

    public int RecentLogsLimit { get; set; } = 5;

    public bool IncludeArchivedSchedules { get; set; } = false;
}

