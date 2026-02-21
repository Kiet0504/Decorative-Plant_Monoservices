using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.PlantLibrary.Commands;
using decorativeplant_be.Application.Features.PlantLibrary.DTOs;
using decorativeplant_be.Domain.Entities;
using MediatR;

namespace decorativeplant_be.Application.Features.PlantLibrary.Handlers;

public class UpdatePlantTaxonomyCommandHandler : IRequestHandler<UpdatePlantTaxonomyCommand, PlantTaxonomyDto>
{
    private readonly IRepositoryFactory _repositoryFactory;
    private readonly IUnitOfWork _unitOfWork;

    public UpdatePlantTaxonomyCommandHandler(IRepositoryFactory repositoryFactory, IUnitOfWork unitOfWork)
    {
        _repositoryFactory = repositoryFactory;
        _unitOfWork = unitOfWork;
    }

    public async Task<PlantTaxonomyDto> Handle(UpdatePlantTaxonomyCommand request, CancellationToken cancellationToken)
    {
        var repo = _repositoryFactory.CreateRepository<PlantTaxonomy>();
        var entity = await repo.GetByIdAsync(request.Id, cancellationToken);

        if (entity == null)
        {
            throw new NotFoundException(nameof(PlantTaxonomy), request.Id);
        }

        entity.ScientificName = request.ScientificName;
        entity.CommonNames = PlantTaxonomyMapper.BuildCommonNames(request.CommonNameEn, request.CommonNameVi);
        entity.TaxonomyInfo = PlantTaxonomyMapper.BuildJson(request.TaxonomyInfo);
        entity.CareInfo = PlantTaxonomyMapper.BuildJson(request.CareInfo);
        entity.GrowthInfo = PlantTaxonomyMapper.BuildJson(request.GrowthInfo);
        entity.ImageUrl = request.ImageUrl;
        entity.Images = PlantTaxonomyMapper.BuildJson(request.Images);
        entity.CategoryId = request.CategoryId;

        await repo.UpdateAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var categoryRepo = _repositoryFactory.CreateRepository<PlantCategory>();
        if (entity.CategoryId.HasValue)
        {
            entity.Category = await categoryRepo.GetByIdAsync(entity.CategoryId.Value, cancellationToken);
        }

        return PlantTaxonomyMapper.ToDto(entity);
    }
}
