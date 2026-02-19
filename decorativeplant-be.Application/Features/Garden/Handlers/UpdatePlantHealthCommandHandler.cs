using decorativeplant_be.Application.Common.DTOs.Garden;
using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Garden;
using decorativeplant_be.Application.Features.Garden.Commands;
using MediatR;

namespace decorativeplant_be.Application.Features.Garden.Handlers;

public class UpdatePlantHealthCommandHandler : IRequestHandler<UpdatePlantHealthCommand, GardenPlantDto>
{
    private readonly IGardenRepository _gardenRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdatePlantHealthCommandHandler(
        IGardenRepository gardenRepository,
        IUnitOfWork unitOfWork)
    {
        _gardenRepository = gardenRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<GardenPlantDto> Handle(UpdatePlantHealthCommand request, CancellationToken cancellationToken)
    {
        var plant = await _gardenRepository.GetPlantByIdAsync(request.Id, includeTaxonomy: false, cancellationToken);
        if (plant == null || plant.UserId != request.UserId)
        {
            throw new NotFoundException("Garden plant", request.Id);
        }

        var mergedDetails = GardenPlantMapper.MergeDetailsJson(
            plant.Details,
            nickname: null,
            location: null,
            source: null,
            adoptedDate: null,
            health: request.Health,
            size: null);

        plant.Details = mergedDetails ?? plant.Details;

        await _gardenRepository.UpdatePlantAsync(plant, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var updated = await _gardenRepository.GetPlantByIdAsync(plant.Id, includeTaxonomy: true, cancellationToken);
        if (updated == null)
        {
            throw new InvalidOperationException("Failed to retrieve updated plant.");
        }

        return GardenPlantMapper.ToDto(updated);
    }
}
