using decorativeplant_be.Application.Common.DTOs.Garden;
using MediatR;

namespace decorativeplant_be.Application.Features.Garden.Commands;

public sealed class UpdateCareScheduleCommand : IRequest<CareScheduleDto>
{
    public Guid UserId { get; set; }

    public Guid ScheduleId { get; set; }

    /// <summary>When set, replaces task_info.</summary>
    public CareScheduleTaskInfoDto? TaskInfo { get; set; }

    public bool? IsActive { get; set; }
}

