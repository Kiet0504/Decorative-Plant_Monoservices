using decorativeplant_be.Application.Common.DTOs.Garden;
using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Garden.Commands;
using decorativeplant_be.Domain.Entities;
using MediatR;

namespace decorativeplant_be.Application.Features.Garden.Handlers;

public sealed class CreateCareScheduleCommandHandler : IRequestHandler<CreateCareScheduleCommand, CareScheduleDto>
{
    private readonly IGardenRepository _gardenRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateCareScheduleCommandHandler(IGardenRepository gardenRepository, IUnitOfWork unitOfWork)
    {
        _gardenRepository = gardenRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<CareScheduleDto> Handle(CreateCareScheduleCommand request, CancellationToken cancellationToken)
    {
        var plant = await _gardenRepository.GetPlantByIdAsync(request.PlantId, includeTaxonomy: false, cancellationToken);
        if (plant == null || plant.UserId != request.UserId)
        {
            throw new NotFoundException("Garden plant", request.PlantId);
        }

        var schedule = new CareSchedule
        {
            Id = Guid.NewGuid(),
            GardenPlantId = request.PlantId,
            IsActive = request.IsActive,
            TaskInfo = CareScheduleMapper.BuildTaskInfoJson(request.TaskInfo)
        };

        await _gardenRepository.AddScheduleAsync(schedule, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return CareScheduleMapper.ToDto(schedule);
    }
}

