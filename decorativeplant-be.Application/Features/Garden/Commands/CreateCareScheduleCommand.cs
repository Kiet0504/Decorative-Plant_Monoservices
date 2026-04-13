using decorativeplant_be.Application.Common.DTOs.Garden;
using MediatR;

namespace decorativeplant_be.Application.Features.Garden.Commands;

public sealed class CreateCareScheduleCommand : IRequest<CareScheduleDto>
{
    public Guid UserId { get; set; }

    public Guid PlantId { get; set; }

    public CareScheduleTaskInfoDto TaskInfo { get; set; } = new();

    public bool IsActive { get; set; } = true;
}

