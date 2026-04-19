using decorativeplant_be.Application.Common.DTOs.Garden;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Inventory.DTOs;
using decorativeplant_be.Application.Features.Inventory.Queries;
using decorativeplant_be.Domain.Entities;
using MediatR;
using System.Linq.Expressions;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace decorativeplant_be.Application.Features.Inventory.Handlers;

public class ListPlantBatchesQueryHandler : IRequestHandler<ListPlantBatchesQuery, PagedResultDto<PlantBatchSummaryDto>>
{
    private readonly IRepositoryFactory _repositoryFactory;
    private readonly IApplicationDbContext _context;

    public ListPlantBatchesQueryHandler(IRepositoryFactory repositoryFactory, IApplicationDbContext context)
    {
        _repositoryFactory = repositoryFactory;
        _context = context;
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

        if (request.BranchId.HasValue)
        {
             var bid = request.BranchId.Value;
             var param = Expression.Parameter(typeof(PlantBatch), "x");
             var body = Expression.AndAlso(
                 Expression.Invoke(filter, param),
                 Expression.Equal(Expression.Property(param, nameof(PlantBatch.BranchId)), Expression.Constant(bid, typeof(Guid?)))
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
        
        IEnumerable<PlantBatch> filteredItems = items;
        
        // Apply Health Status Filter (In-memory for JSONB field)
        if (!string.IsNullOrEmpty(request.HealthStatus) && request.HealthStatus != "All Status")
        {
            var normalizedRequest = request.HealthStatus.Replace(" ", "").Replace("_", "").ToLower();
            filteredItems = filteredItems.Where(x => 
                x.Specs != null && 
                x.Specs.RootElement.TryGetProperty("health_status", out var hp) && 
                hp.GetString() != null &&
                hp.GetString()!.Replace(" ", "").Replace("_", "").ToLower() == normalizedRequest);
            totalCount = filteredItems.Count();
        }

        // Apply Sorting (Stable sort with secondary ID key)
        if (!string.IsNullOrEmpty(request.SortOrder))
        {
            if (request.SortOrder.ToLower() == "asc")
                filteredItems = filteredItems.OrderBy(x => x.CreatedAt ?? DateTime.MinValue).ThenBy(x => x.Id);
            else // "desc" or default
                filteredItems = filteredItems.OrderByDescending(x => x.CreatedAt ?? DateTime.MinValue).ThenByDescending(x => x.Id);
        }
        else
        {
            filteredItems = filteredItems.OrderByDescending(x => x.CreatedAt ?? DateTime.MinValue).ThenByDescending(x => x.Id);
        }

        // Final count after all filters
        var finalItems = filteredItems.ToList();
        totalCount = finalItems.Count;
        
        var pagedItems = finalItems
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

            if (item.BranchId.HasValue && item.Branch == null)
            {
                var branchRepo = _repositoryFactory.CreateRepository<decorativeplant_be.Domain.Entities.Branch>();
                item.Branch = await branchRepo.GetByIdAsync(item.BranchId.Value, cancellationToken);
            }

            // Load Stocks for Aggregation
            if (item.BatchStocks == null || !item.BatchStocks.Any())
            {
                item.BatchStocks = await _context.BatchStocks
                    .Include(bs => bs.Location)
                    .Where(bs => bs.BatchId == item.Id)
                    .ToListAsync(cancellationToken);
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
