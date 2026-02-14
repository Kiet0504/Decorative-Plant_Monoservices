using decorativeplant_be.Application.Common.DTOs.Garden;
using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Garden;
using decorativeplant_be.Application.Features.Garden.Commands;
using decorativeplant_be.Domain.Entities;
using MediatR;

namespace decorativeplant_be.Application.Features.Garden.Handlers;

public class AddCareLogCommandHandler : IRequestHandler<AddCareLogCommand, CareLogDto>
{
    private readonly IGardenRepository _gardenRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AddCareLogCommandHandler(
        IGardenRepository gardenRepository,
        IUnitOfWork unitOfWork)
    {
        _gardenRepository = gardenRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<CareLogDto> Handle(AddCareLogCommand request, CancellationToken cancellationToken)
    {
        var plant = await _gardenRepository.GetPlantByIdAsync(request.PlantId, includeTaxonomy: false, cancellationToken);
        if (plant == null || plant.UserId != request.UserId)
        {
            throw new NotFoundException("Garden plant", request.PlantId);
        }

        var logInfo = CareLogMapper.BuildLogInfoJson(
            request.ActionType,
            request.Description,
            request.Products,
            request.Observations,
            request.Mood);

        var images = CareLogMapper.BuildImagesJson(request.Images);

        var careLog = new CareLog
        {
            Id = Guid.NewGuid(),
            GardenPlantId = request.PlantId,
            ScheduleId = request.ScheduleId,
            LogInfo = logInfo,
            Images = images,
            PerformedAt = request.PerformedAt ?? DateTime.UtcNow
        };

        await _gardenRepository.AddCareLogAsync(careLog, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return CareLogMapper.ToDto(careLog);
    }
}
