using decorativeplant_be.Application.Common.DTOs.Garden;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Inventory.DTOs;
using decorativeplant_be.Application.Features.Inventory.Queries;
using decorativeplant_be.Domain.Entities;
using MediatR;
using System.Linq.Expressions;

namespace decorativeplant_be.Application.Features.Inventory.Handlers;

public class ListPlantBatchesQueryHandler : IRequestHandler<ListPlantBatchesQuery, PagedResultDto<PlantBatchSummaryDto>>
{
    private readonly IRepositoryFactory _repositoryFactory;

    public ListPlantBatchesQueryHandler(IRepositoryFactory repositoryFactory)
    {
        _repositoryFactory = repositoryFactory;
    }

    public async Task<PagedResultDto<PlantBatchSummaryDto>> Handle(ListPlantBatchesQuery request, CancellationToken cancellationToken)
    {
        var repo = _repositoryFactory.CreateRepository<PlantBatch>();

        Expression<Func<PlantBatch, bool>> filter = x => true;

        if (request.TaxonomyId.HasValue)
        {
            var tid = request.TaxonomyId.Value;
            var param = Expression.Parameter(typeof(PlantBatch), "x");
            var body = Expression.AndAlso(
                Expression.Invoke(filter, param),
                Expression.Equal(Expression.Property(param, nameof(PlantBatch.TaxonomyId)), Expression.Constant(tid, typeof(Guid?)))
            );
            filter = Expression.Lambda<Func<PlantBatch, bool>>(body, param);
        }

        if (request.SupplierId.HasValue)
        {
             var sid = request.SupplierId.Value;
             var param = Expression.Parameter(typeof(PlantBatch), "x");
             var body = Expression.AndAlso(
                 Expression.Invoke(filter, param),
                 Expression.Equal(Expression.Property(param, nameof(PlantBatch.SupplierId)), Expression.Constant(sid, typeof(Guid?)))
             );
             filter = Expression.Lambda<Func<PlantBatch, bool>>(body, param);
        }

        if (!string.IsNullOrEmpty(request.SearchTerm))
        {
            var term = request.SearchTerm.ToLower();
            // Search by BatchCode. Searching by Species Name via Taxonomy might require Join or separate ID lookup.
            // For simplicity, search BatchCode. If we want Species name, we'd need to fetch Taxonomies matching name first (not done here for perf/complexity reasons in generic repo).
            // Let's assume BatchCode search.
            
            Expression<Func<PlantBatch, bool>> searchFilter = x => x.BatchCode != null && x.BatchCode.ToLower().Contains(term);
             
            var param = Expression.Parameter(typeof(PlantBatch), "x");
            var body = Expression.AndAlso(
                Expression.Invoke(filter, param),
                Expression.Invoke(searchFilter, param)
            );
            filter = Expression.Lambda<Func<PlantBatch, bool>>(body, param);
        }

        var totalCount = await repo.CountAsync(filter, cancellationToken);
        var items = await repo.FindAsync(filter, cancellationToken);
        
        var pagedItems = items
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        // Populate Taxonomy Name for display
        var taxRepo = _repositoryFactory.CreateRepository<PlantTaxonomy>();
        foreach (var item in pagedItems)
        {
            if (item.TaxonomyId.HasValue && item.Taxonomy == null)
            {
                item.Taxonomy = await taxRepo.GetByIdAsync(item.TaxonomyId.Value, cancellationToken);
            }
        }

        var dtos = pagedItems.Select(PlantBatchMapper.ToSummaryDto).ToList();

        return new PagedResultDto<PlantBatchSummaryDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}
