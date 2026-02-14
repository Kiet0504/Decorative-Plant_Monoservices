using decorativeplant_be.Application.Common.DTOs.Garden;
using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Garden;
using decorativeplant_be.Application.Features.Garden.Commands;
using decorativeplant_be.Domain.Entities;
using MediatR;

namespace decorativeplant_be.Application.Features.Garden.Handlers;

public class CreateGardenPlantCommandHandler : IRequestHandler<CreateGardenPlantCommand, GardenPlantDto>
{
    private readonly IGardenRepository _gardenRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRepositoryFactory _repositoryFactory;

    public CreateGardenPlantCommandHandler(
        IGardenRepository gardenRepository,
        IUnitOfWork unitOfWork,
        IRepositoryFactory repositoryFactory)
    {
        _gardenRepository = gardenRepository;
        _unitOfWork = unitOfWork;
        _repositoryFactory = repositoryFactory;
    }

    public async Task<GardenPlantDto> Handle(CreateGardenPlantCommand request, CancellationToken cancellationToken)
    {
        if (request.TaxonomyId.HasValue)
        {
            var taxonomyRepo = _repositoryFactory.CreateRepository<PlantTaxonomy>();
            var exists = await taxonomyRepo.ExistsAsync(t => t.Id == request.TaxonomyId.Value, cancellationToken);
            if (!exists)
            {
                throw new ValidationException("Taxonomy not found.");
            }
        }

        var details = GardenPlantMapper.BuildDetailsJson(
            request.Nickname,
            request.Location,
            request.Source,
            request.AdoptedDate,
            request.Health,
            request.Size,
            milestones: null);

        var plant = new GardenPlant
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            TaxonomyId = request.TaxonomyId,
            Details = details,
            ImageUrl = request.ImageUrl,
            IsArchived = false,
            CreatedAt = DateTime.UtcNow
        };

        await _gardenRepository.AddPlantAsync(plant, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var created = await _gardenRepository.GetPlantByIdAsync(plant.Id, includeTaxonomy: true, cancellationToken);
        if (created == null)
        {
            throw new InvalidOperationException("Failed to retrieve created plant.");
        }

        return GardenPlantMapper.ToDto(created);
    }
}
