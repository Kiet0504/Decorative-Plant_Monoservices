using decorativeplant_be.Application.Common.DTOs.Garden;
using MediatR;

namespace decorativeplant_be.Application.Features.Garden.Commands;

public sealed class BulkCreateCareSchedulesCommand : IRequest<IReadOnlyList<CareScheduleDto>>
{
    public Guid UserId { get; set; }

    public Guid PlantId { get; set; }

    public List<CareScheduleTaskInfoDto> Tasks { get; set; } = new();
}

