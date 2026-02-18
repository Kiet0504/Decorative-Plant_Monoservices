using decorativeplant_be.Application.Common.DTOs.Garden;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.PlantLibrary.DTOs;
using decorativeplant_be.Application.Features.PlantLibrary.Queries;
using decorativeplant_be.Domain.Entities;
using MediatR;
using System.Linq.Expressions;

namespace decorativeplant_be.Application.Features.PlantLibrary.Handlers;

public class ListPlantTaxonomiesQueryHandler : IRequestHandler<ListPlantTaxonomiesQuery, PagedResultDto<PlantTaxonomySummaryDto>>
{
    private readonly IRepositoryFactory _repositoryFactory;

    public ListPlantTaxonomiesQueryHandler(IRepositoryFactory repositoryFactory)
    {
        _repositoryFactory = repositoryFactory;
    }

    public async Task<PagedResultDto<PlantTaxonomySummaryDto>> Handle(ListPlantTaxonomiesQuery request, CancellationToken cancellationToken)
    {
        var repo = _repositoryFactory.CreateRepository<PlantTaxonomy>();
        var categoryRepo = _repositoryFactory.CreateRepository<PlantCategory>();

        Expression<Func<PlantTaxonomy, bool>> filter = x => true;

        if (request.CategoryId.HasValue)
        {
            var cid = request.CategoryId.Value;
            filter = x => x.CategoryId == cid;
        }

        if (!string.IsNullOrEmpty(request.SearchTerm))
        {
            var term = request.SearchTerm.ToLower();
            // Note: JSONB search in generic repo via LINQ might be limited depending on provider.
            // Searching ScientificName is safe. CommonNames is JSONB.
            // Simple approach: Search ScientificName OR assume we can search JSON string representation (inefficient but works for small datasets)
            // Or only search ScientificName for now.
            // Let's chain predicates.
            
            Expression<Func<PlantTaxonomy, bool>> searchFilter = x => x.ScientificName.ToLower().Contains(term);
            
            // Combine filters (basic AND)
            var param = Expression.Parameter(typeof(PlantTaxonomy), "x");
            var body = Expression.AndAlso(
                Expression.Invoke(filter, param),
                Expression.Invoke(searchFilter, param)
            );
            filter = Expression.Lambda<Func<PlantTaxonomy, bool>>(body, param);
        }
        
        var totalCount = await repo.CountAsync(filter, cancellationToken);
        var items = await repo.FindAsync(filter, cancellationToken); 
        
        // Pagination logic here (manual since FindAsync returns IEnumerable typically, or IQueryable? Repository returns IEnumerable usually)
        // If Repository returns IEnumerable, we are fetching ALL match then paging in memory. Not ideal for large sets but ok for MVP.
        // Actually Repository FindAsync returns IEnumerable<T>.
        // Ideally we should push pagination to DB. But assuming small dataset for Taxonomies.
        
        var pagedItems = items
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        // Populate Categories for display
        foreach(var item in pagedItems)
        {
            if (item.CategoryId.HasValue && item.Category == null)
            {
                item.Category = await categoryRepo.GetByIdAsync(item.CategoryId.Value, cancellationToken);
            }
        }

        var dtos = pagedItems.Select(PlantTaxonomyMapper.ToSummaryDto).ToList();

        return new PagedResultDto<PlantTaxonomySummaryDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}
