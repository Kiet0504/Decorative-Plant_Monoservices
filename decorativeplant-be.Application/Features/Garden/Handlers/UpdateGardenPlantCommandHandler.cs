using decorativeplant_be.Application.Common.DTOs.Garden;
using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Garden;
using decorativeplant_be.Application.Features.Garden.Commands;
using MediatR;

namespace decorativeplant_be.Application.Features.Garden.Handlers;

public class UpdateGardenPlantCommandHandler : IRequestHandler<UpdateGardenPlantCommand, GardenPlantDto>
{
    private readonly IGardenRepository _gardenRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRepositoryFactory _repositoryFactory;

    public UpdateGardenPlantCommandHandler(
        IGardenRepository gardenRepository,
        IUnitOfWork unitOfWork,
        IRepositoryFactory repositoryFactory)
    {
        _gardenRepository = gardenRepository;
        _unitOfWork = unitOfWork;
        _repositoryFactory = repositoryFactory;
    }

    public async Task<GardenPlantDto> Handle(UpdateGardenPlantCommand request, CancellationToken cancellationToken)
    {
        var plant = await _gardenRepository.GetPlantByIdAsync(request.Id, includeTaxonomy: false, cancellationToken);
        if (plant == null || plant.UserId != request.UserId)
        {
            throw new NotFoundException("Garden plant", request.Id);
        }

        if (request.TaxonomyId.HasValue)
        {
            var taxonomyRepo = _repositoryFactory.CreateRepository<Domain.Entities.PlantTaxonomy>();
            var exists = await taxonomyRepo.ExistsAsync(t => t.Id == request.TaxonomyId.Value, cancellationToken);
            if (!exists)
            {
                throw new ValidationException("Taxonomy not found.");
            }
        }

        var mergedDetails = GardenPlantMapper.MergeDetailsJson(
            plant.Details,
            request.Nickname,
            request.Location,
            request.Source,
            request.AdoptedDate,
            request.Health,
            request.Size);

        plant.TaxonomyId = request.TaxonomyId ?? plant.TaxonomyId;
        plant.Details = mergedDetails ?? plant.Details;
        plant.ImageUrl = request.ImageUrl ?? plant.ImageUrl;

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
