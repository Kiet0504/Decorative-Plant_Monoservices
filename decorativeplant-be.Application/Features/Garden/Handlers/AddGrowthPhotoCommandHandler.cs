using decorativeplant_be.Application.Common.DTOs.Garden;
using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Garden.Commands;
using decorativeplant_be.Domain.Entities;
using MediatR;

namespace decorativeplant_be.Application.Features.Garden.Handlers;

public class AddGrowthPhotoCommandHandler : IRequestHandler<AddGrowthPhotoCommand, GrowthPhotoEntryDto>
{
    private readonly IGardenRepository _gardenRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AddGrowthPhotoCommandHandler(IGardenRepository gardenRepository, IUnitOfWork unitOfWork)
    {
        _gardenRepository = gardenRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<GrowthPhotoEntryDto> Handle(AddGrowthPhotoCommand request, CancellationToken cancellationToken)
    {
        var plant = await _gardenRepository.GetPlantByIdAsync(request.PlantId, includeTaxonomy: false, cancellationToken);
        if (plant == null || plant.UserId != request.UserId)
        {
            throw new NotFoundException("Garden plant", request.PlantId);
        }

        var performedAt = request.PerformedAt ?? DateTime.UtcNow;

        var logInfo = CareLogMapper.BuildLogInfoJson(
            actionType: "photo",
            description: request.Caption,
            products: null,
            observations: null,
            mood: null);

        var images = CareLogMapper.BuildImagesJson(new List<CareLogImageDto>
        {
            new()
            {
                Url = request.ImageUrl,
                Caption = request.Caption,
                AiTags = new List<string>()
            }
        });

        var careLog = new CareLog
        {
            Id = Guid.NewGuid(),
            GardenPlantId = request.PlantId,
            ScheduleId = null,
            LogInfo = logInfo,
            Images = images,
            PerformedAt = performedAt
        };

        await _gardenRepository.AddCareLogAsync(careLog, cancellationToken);

        if (request.SetAsAvatar)
        {
            plant.ImageUrl = request.ImageUrl;
            await _gardenRepository.UpdatePlantAsync(plant, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new GrowthPhotoEntryDto
        {
            CareLogId = careLog.Id,
            ImageUrl = request.ImageUrl,
            Caption = request.Caption,
            PerformedAt = performedAt
        };
    }
}

