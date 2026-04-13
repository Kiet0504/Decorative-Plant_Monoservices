using decorativeplant_be.Application.Common.DTOs.Garden;
using MediatR;

namespace decorativeplant_be.Application.Features.Garden.Queries;

public sealed class GenerateGardenPlantAiSchedulePlanQuery : IRequest<AiSchedulePlanDto>
{
    public Guid UserId { get; set; }
    public Guid PlantId { get; set; }

    /// <summary>Optional. If omitted, planner returns tasks starting from now.</summary>
    public DateTime? StartAtUtc { get; set; }

    /// <summary>Number of days to plan ahead (e.g. 30). Defaults to 30.</summary>
    public int HorizonDays { get; set; } = 30;
}

