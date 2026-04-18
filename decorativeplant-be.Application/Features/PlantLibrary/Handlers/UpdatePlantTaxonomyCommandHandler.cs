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

        var existingName = request.ScientificName.Trim().ToLower();
        var existing = await repo.FirstOrDefaultAsync(x => x.ScientificName.ToLower() == existingName && x.Id != request.Id, cancellationToken);
        if (existing != null)
        {
            throw new BadRequestException($"A plant taxonomy with the scientific name '{request.ScientificName}' already exists.");
        }

        // Category Resolution
        Guid? finalCategoryId = request.CategoryId;
        if (!string.IsNullOrEmpty(request.CategoryName))
        {
            var categoryRepo = _repositoryFactory.CreateRepository<PlantCategory>();
            // Load all categories to perform robust in-memory matching
            var allCategories = await categoryRepo.FindAsync(c => true, cancellationToken);
            var searchName = request.CategoryName.Trim().ToLower().Replace(" ", "_");
            
            var category = allCategories.FirstOrDefault(c => 
                (c.Slug != null && c.Slug.ToLower() == searchName) ||
                (c.Name != null && c.Name.Trim().ToLower() == request.CategoryName.Trim().ToLower()) ||
                (c.Name != null && c.Name.Replace(" ", "").Equals(request.CategoryName.Replace(" ", ""), StringComparison.OrdinalIgnoreCase)));
            
            if (category != null)
            {
                finalCategoryId = category.Id;
            }
        }
        
        if (finalCategoryId.HasValue)
        {
            entity.CategoryId = finalCategoryId;
        }

        entity.ScientificName = request.ScientificName;
        entity.CommonNames = PlantTaxonomyMapper.BuildCommonNames(request.CommonNameEn, request.CommonNameVi);
        entity.TaxonomyInfo = PlantTaxonomyMapper.BuildJson(request.TaxonomyInfo);
        entity.CareInfo = PlantTaxonomyMapper.BuildJson(request.CareInfo);
        entity.GrowthInfo = PlantTaxonomyMapper.BuildJson(request.GrowthInfo);
        entity.ImageUrl = request.ImageUrl;

        await repo.UpdateAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Include category for DTO response
        if (entity.CategoryId.HasValue)
        {
            var categoryRepo = _repositoryFactory.CreateRepository<PlantCategory>();
            entity.Category = await categoryRepo.GetByIdAsync(entity.CategoryId.Value, cancellationToken);
        }

        return PlantTaxonomyMapper.ToDto(entity);
    }
}
