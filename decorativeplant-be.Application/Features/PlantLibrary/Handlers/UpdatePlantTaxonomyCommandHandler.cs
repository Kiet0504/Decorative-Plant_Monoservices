using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.PlantLibrary.Commands;
using decorativeplant_be.Application.Features.PlantLibrary.DTOs;
using decorativeplant_be.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace decorativeplant_be.Application.Features.PlantLibrary.Handlers;

public class UpdatePlantTaxonomyCommandHandler : IRequestHandler<UpdatePlantTaxonomyCommand, PlantTaxonomyDto>
{
    private readonly IRepositoryFactory _repositoryFactory;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IApplicationDbContext _context;

    public UpdatePlantTaxonomyCommandHandler(IRepositoryFactory repositoryFactory, IUnitOfWork unitOfWork, IApplicationDbContext context)
    {
        _repositoryFactory = repositoryFactory;
        _unitOfWork = unitOfWork;
        _context = context;
    }

    public async Task<PlantTaxonomyDto> Handle(UpdatePlantTaxonomyCommand request, CancellationToken cancellationToken)
    {
        var repo = _repositoryFactory.CreateRepository<PlantTaxonomy>();
        var entity = await repo.GetByIdAsync(request.Id, cancellationToken);

        if (entity == null)
        {
            throw new NotFoundException(nameof(PlantTaxonomy), request.Id);
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
        entity.DefaultPrice = request.DefaultPrice;

        await repo.UpdateAsync(entity, cancellationToken);

        // --- NEW: Price Propagation Logic ---
        if (request.DefaultPrice.HasValue)
        {
            var newPriceStr = request.DefaultPrice.Value.ToString("0"); // Format for ProductListing JSONB
            
            // Find all listings for this species across all branches
            var affectedListings = await _context.ProductListings
                .Include(pl => pl.Batch)
                .Where(pl => pl.Batch != null && pl.Batch.TaxonomyId == entity.Id)
                .ToListAsync(cancellationToken);

            foreach (var listing in affectedListings)
            {
                if (listing.ProductInfo != null)
                {
                    var productInfo = new Dictionary<string, object?>();
                    foreach (var prop in listing.ProductInfo.RootElement.EnumerateObject())
                    {
                        productInfo[prop.Name] = GetJsonValue(prop.Value);
                    }
                    
                    productInfo["price"] = newPriceStr;
                    listing.ProductInfo = System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(productInfo));
                }
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Include category for DTO response
        if (entity.CategoryId.HasValue)
        {
            var categoryRepo = _repositoryFactory.CreateRepository<PlantCategory>();
            entity.Category = await categoryRepo.GetByIdAsync(entity.CategoryId.Value, cancellationToken);
        }

        return PlantTaxonomyMapper.ToDto(entity);
    }

    private static object? GetJsonValue(System.Text.Json.JsonElement element) => element.ValueKind switch
    {
        System.Text.Json.JsonValueKind.String => element.GetString(),
        System.Text.Json.JsonValueKind.Number => element.TryGetInt32(out var i) ? i : element.GetDouble(),
        System.Text.Json.JsonValueKind.True => true,
        System.Text.Json.JsonValueKind.False => false,
        System.Text.Json.JsonValueKind.Null => null,
        _ => element
    };
}
