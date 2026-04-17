using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.PlantLibrary.Commands;
using decorativeplant_be.Application.Features.PlantLibrary.DTOs;
using decorativeplant_be.Domain.Entities;
using MediatR;
using System.Text.Json;

namespace decorativeplant_be.Application.Features.PlantLibrary.Handlers;

public class CreatePlantTaxonomyCommandHandler : IRequestHandler<CreatePlantTaxonomyCommand, PlantTaxonomyDto>
{
    private readonly IRepositoryFactory _repositoryFactory;
    private readonly IUnitOfWork _unitOfWork;

    public CreatePlantTaxonomyCommandHandler(IRepositoryFactory repositoryFactory, IUnitOfWork unitOfWork)
    {
        _repositoryFactory = repositoryFactory;
        _unitOfWork = unitOfWork;
    }

    public async Task<PlantTaxonomyDto> Handle(CreatePlantTaxonomyCommand request, CancellationToken cancellationToken)
    {
        Guid? finalCategoryId = request.CategoryId;
        if (!finalCategoryId.HasValue && !string.IsNullOrEmpty(request.CategoryName))
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

        var commonNamesJson = PlantTaxonomyMapper.BuildCommonNames(request.CommonNameEn, request.CommonNameVi);
        var taxonomyInfoJson = PlantTaxonomyMapper.BuildJson(request.TaxonomyInfo);
        var careInfoJson = PlantTaxonomyMapper.BuildJson(request.CareInfo);
        var growthInfoJson = PlantTaxonomyMapper.BuildJson(request.GrowthInfo);

        var entity = new PlantTaxonomy
        {
            Id = Guid.NewGuid(),
            ScientificName = request.ScientificName,
            CommonNames = commonNamesJson,
            TaxonomyInfo = taxonomyInfoJson,
            CareInfo = careInfoJson,
            GrowthInfo = growthInfoJson,
            ImageUrl = request.ImageUrl,
            DefaultPrice = request.DefaultPrice,
            CategoryId = finalCategoryId
        };

        var repo = _repositoryFactory.CreateRepository<PlantTaxonomy>();
        await repo.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Fetch again to include category name if needed, or structured response
        if (entity.CategoryId.HasValue)
        {
            var categoryRepo = _repositoryFactory.CreateRepository<PlantCategory>();
            entity.Category = await categoryRepo.GetByIdAsync(entity.CategoryId.Value, cancellationToken);
        }

        return PlantTaxonomyMapper.ToDto(entity);
    }
}
