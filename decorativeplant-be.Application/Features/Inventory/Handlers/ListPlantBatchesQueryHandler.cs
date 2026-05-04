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

        List<Guid?>? batchIdsInLocation = null;
        if (request.LocationId.HasValue)
        {
             var lid = request.LocationId.Value;
             batchIdsInLocation = await _context.BatchStocks
                 .Where(bs => bs.LocationId == lid)
                 .Select(bs => bs.BatchId)
                 .Distinct()
                 .ToListAsync(cancellationToken);
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
        
        if (batchIdsInLocation != null)
        {
            filteredItems = filteredItems.Where(x => batchIdsInLocation.Contains(x.Id)).ToList();
            totalCount = filteredItems.Count();
        }
        
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

        // 1. Collect IDs for batch fetching
        var taxonomyIds = pagedItems.Where(i => i.TaxonomyId.HasValue && i.Taxonomy == null).Select(i => i.TaxonomyId!.Value).Distinct().ToList();
        var branchIds = pagedItems.Where(i => i.BranchId.HasValue && i.Branch == null).Select(i => i.BranchId!.Value).Distinct().ToList();
        var batchIds = pagedItems.Select(i => i.Id).ToList();

        // 2. Batch fetch related data
        var taxonomies = taxonomyIds.Any() 
            ? await _context.PlantTaxonomies.Include(t => t.Category).Where(t => taxonomyIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, cancellationToken)
            : new Dictionary<Guid, PlantTaxonomy>();

        var branches = branchIds.Any()
            ? await _context.Branches.Where(b => branchIds.Contains(b.Id)).ToDictionaryAsync(b => b.Id, cancellationToken)
            : new Dictionary<Guid, decorativeplant_be.Domain.Entities.Branch>();

        var allBatchStocks = await _context.BatchStocks
            .Include(bs => bs.Location)
            .Where(bs => batchIds.Contains(bs.BatchId ?? Guid.Empty))
            .ToListAsync(cancellationToken);

        var stocksByBatch = allBatchStocks.GroupBy(bs => bs.BatchId).ToDictionary(g => g.Key, g => g.ToList());

        // 3. Assign fetched data back to items
        foreach (var item in pagedItems)
        {
            if (item.TaxonomyId.HasValue && item.Taxonomy == null && taxonomies.TryGetValue(item.TaxonomyId.Value, out var tax))
            {
                item.Taxonomy = tax;
            }

            if (item.BranchId.HasValue && item.Branch == null && branches.TryGetValue(item.BranchId.Value, out var branch))
            {
                item.Branch = branch;
            }

            if (item.BatchStocks == null || !item.BatchStocks.Any())
            {
                if (stocksByBatch.TryGetValue(item.Id, out var stocks))
                {
                    item.BatchStocks = stocks;
                }
                else
                {
                    item.BatchStocks = new List<BatchStock>();
                }
            }
        }

        var dtos = pagedItems.Select(PlantBatchMapper.ToSummaryDto).ToList();

        if (request.LocationId.HasValue)
        {
            var lid = request.LocationId.Value;
            foreach (var dto in dtos)
            {
                var item = pagedItems.First(x => x.Id == dto.Id);
                var localStock = item.BatchStocks?.FirstOrDefault(s => s.LocationId == lid);
                if (localStock?.Quantities != null)
                {
                    var root = localStock.Quantities.RootElement;
                    int localQty = (int)((root.TryGetProperty("reserved_quantity", out var rq) && rq.ValueKind == JsonValueKind.Number) ? rq.GetDouble() : 0);
                    
                    // Override fields for local context display
                    dto.Quantity = localQty;
                    dto.CurrentTotalQuantity = localQty;
                    dto.ReservedQuantity = localQty;
                }
            }
            // Final safety filter: remove items that ended up having 0 local quantity
            dtos = dtos.Where(x => x.CurrentTotalQuantity > 0).ToList();
        }

        return new PagedResultDto<PlantBatchSummaryDto>
        {
            Items = dtos,
            TotalCount = dtos.Count, // Update count if we filtered
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}
