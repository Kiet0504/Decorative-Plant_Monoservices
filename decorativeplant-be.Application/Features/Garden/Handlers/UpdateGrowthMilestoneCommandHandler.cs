using decorativeplant_be.Application.Common.DTOs.Garden;
using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Garden;
using decorativeplant_be.Application.Features.Garden.Commands;
using MediatR;

namespace decorativeplant_be.Application.Features.Garden.Handlers;

public class UpdateGrowthMilestoneCommandHandler : IRequestHandler<UpdateGrowthMilestoneCommand, GrowthMilestoneDto>
{
    private readonly IGardenRepository _gardenRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateGrowthMilestoneCommandHandler(
        IGardenRepository gardenRepository,
        IUnitOfWork unitOfWork)
    {
        _gardenRepository = gardenRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<GrowthMilestoneDto> Handle(UpdateGrowthMilestoneCommand request, CancellationToken cancellationToken)
    {
        var plant = await _gardenRepository.GetPlantByIdAsync(request.PlantId, includeTaxonomy: false, cancellationToken);
        if (plant == null || plant.UserId != request.UserId)
        {
            throw new NotFoundException("Garden plant", request.PlantId);
        }

        var updated = GardenPlantMapper.UpdateMilestone(
            plant.Details,
            request.MilestoneId,
            request.Type,
            request.OccurredAt,
            request.Notes,
            request.ImageUrl);

        plant.Details = updated ?? plant.Details;
        await _gardenRepository.UpdatePlantAsync(plant, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var details = GardenPlantMapper.DeserializeDetails(plant.Details);
        var milestone = details.Milestones?.FirstOrDefault(m => m.Id == request.MilestoneId);
        if (milestone == null)
        {
            throw new NotFoundException("Growth milestone", request.MilestoneId);
        }

        return milestone;
    }
}
