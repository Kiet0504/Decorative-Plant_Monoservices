using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.PlantLibrary.DTOs;
using decorativeplant_be.Application.Features.PlantLibrary.Queries;
using decorativeplant_be.Domain.Entities;
using MediatR;

namespace decorativeplant_be.Application.Features.PlantLibrary.Handlers;

public class GetPlantTaxonomyQueryHandler : IRequestHandler<GetPlantTaxonomyQuery, PlantTaxonomyDto>
{
    private readonly IRepositoryFactory _repositoryFactory;

    public GetPlantTaxonomyQueryHandler(IRepositoryFactory repositoryFactory)
    {
        _repositoryFactory = repositoryFactory;
    }

    public async Task<PlantTaxonomyDto> Handle(GetPlantTaxonomyQuery request, CancellationToken cancellationToken)
    {
        var repo = _repositoryFactory.CreateRepository<PlantTaxonomy>();
        
        // Need to include Category. Since implementation of Repository is generic, usage depends on if we can include.
        // The standard Repository<T> might not support Include easily without specification pattern or custom method.
        // For now, let's fetch category separately or assume lazy loading (not recommended) or just fetch base.
        // Actually, let's fetch normally. If we need category name, we might need a custom query or strict ID based fetch.
        // Refactoring generic repo to support includes is out of scope.
        // Let's optimize by fetching category separately IF needed, or just return what we have.
        // The mapper uses entity.Category?.Name.
        
        var entity = await repo.GetByIdAsync(request.Id, cancellationToken);
        if (entity == null)
        {
            throw new NotFoundException(nameof(PlantTaxonomy), request.Id);
        }

        if (entity.CategoryId.HasValue && entity.Category == null)
        {
             var categoryRepo = _repositoryFactory.CreateRepository<PlantCategory>();
             entity.Category = await categoryRepo.GetByIdAsync(entity.CategoryId.Value, cancellationToken);
        }

        return PlantTaxonomyMapper.ToDto(entity);
    }
}
