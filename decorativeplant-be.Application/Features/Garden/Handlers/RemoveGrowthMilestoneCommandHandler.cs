using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Garden;
using decorativeplant_be.Application.Features.Garden.Commands;
using MediatR;

namespace decorativeplant_be.Application.Features.Garden.Handlers;

public class RemoveGrowthMilestoneCommandHandler : IRequestHandler<RemoveGrowthMilestoneCommand, Unit>
{
    private readonly IGardenRepository _gardenRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RemoveGrowthMilestoneCommandHandler(
        IGardenRepository gardenRepository,
        IUnitOfWork unitOfWork)
    {
        _gardenRepository = gardenRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Unit> Handle(RemoveGrowthMilestoneCommand request, CancellationToken cancellationToken)
    {
        var plant = await _gardenRepository.GetPlantByIdAsync(request.PlantId, includeTaxonomy: false, cancellationToken);
        if (plant == null || plant.UserId != request.UserId)
        {
            throw new NotFoundException("Garden plant", request.PlantId);
        }

        var details = GardenPlantMapper.DeserializeDetails(plant.Details);
        var exists = details.Milestones?.Any(m => m.Id == request.MilestoneId) ?? false;
        if (!exists)
        {
            throw new NotFoundException("Growth milestone", request.MilestoneId);
        }

        plant.Details = GardenPlantMapper.RemoveMilestone(plant.Details, request.MilestoneId) ?? plant.Details;
        await _gardenRepository.UpdatePlantAsync(plant, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
