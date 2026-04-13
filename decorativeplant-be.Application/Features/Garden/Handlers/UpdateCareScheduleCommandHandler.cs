using decorativeplant_be.Application.Common.DTOs.Garden;
using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Garden.Commands;
using MediatR;

namespace decorativeplant_be.Application.Features.Garden.Handlers;

public sealed class UpdateCareScheduleCommandHandler : IRequestHandler<UpdateCareScheduleCommand, CareScheduleDto>
{
    private readonly IGardenRepository _gardenRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateCareScheduleCommandHandler(IGardenRepository gardenRepository, IUnitOfWork unitOfWork)
    {
        _gardenRepository = gardenRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<CareScheduleDto> Handle(UpdateCareScheduleCommand request, CancellationToken cancellationToken)
    {
        var schedule = await _gardenRepository.GetScheduleByIdAsync(request.ScheduleId, cancellationToken);
        if (schedule == null)
        {
            throw new NotFoundException("Care schedule", request.ScheduleId);
        }

        // Ownership check via plant
        if (schedule.GardenPlantId == null)
        {
            throw new ValidationException("Schedule is not linked to a plant.");
        }
        var plant = await _gardenRepository.GetPlantByIdAsync(schedule.GardenPlantId.Value, includeTaxonomy: false, cancellationToken);
        if (plant == null || plant.UserId != request.UserId)
        {
            throw new NotFoundException("Garden plant", schedule.GardenPlantId.Value);
        }

        if (request.IsActive.HasValue)
        {
            schedule.IsActive = request.IsActive.Value;
        }

        if (request.TaskInfo != null)
        {
            schedule.TaskInfo = CareScheduleMapper.BuildTaskInfoJson(request.TaskInfo);
        }

        await _gardenRepository.UpdateScheduleAsync(schedule, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return CareScheduleMapper.ToDto(schedule);
    }
}

