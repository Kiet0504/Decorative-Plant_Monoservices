using decorativeplant_be.Application.Common.DTOs.Garden;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.PlantLibrary.DTOs;
using decorativeplant_be.Application.Features.PlantLibrary.Queries;
using decorativeplant_be.Domain.Entities;
using MediatR;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace decorativeplant_be.Application.Features.PlantLibrary.Handlers;

public class ListPlantTaxonomiesQueryHandler : IRequestHandler<ListPlantTaxonomiesQuery, PagedResultDto<PlantTaxonomySummaryDto>>
{
    private readonly IRepositoryFactory _repositoryFactory;
    private readonly IApplicationDbContext _context;

    public ListPlantTaxonomiesQueryHandler(IRepositoryFactory repositoryFactory, IApplicationDbContext context)
    {
        _repositoryFactory = repositoryFactory;
        _context = context;
    }

    public async Task<PagedResultDto<PlantTaxonomySummaryDto>> Handle(ListPlantTaxonomiesQuery request, CancellationToken cancellationToken)
    {
        var categoryRepo = _repositoryFactory.CreateRepository<PlantCategory>();

        // We use IApplicationDbContext directly for complex taxonomies filtering (to support OnlyWithActiveListings)
        var q = _context.PlantTaxonomies.AsQueryable();

        if (request.CategoryId.HasValue)
        {
            q = q.Where(x => x.CategoryId == request.CategoryId.Value);
        }

        if (!string.IsNullOrEmpty(request.SearchTerm))
        {
            var term = request.SearchTerm.ToLower();
            q = q.Where(x => x.ScientificName.ToLower().Contains(term));
        }

        if (request.OnlyWithActiveListings)
        {
            // Fetch taxonomies that have AT LEAST ONE product listing
            // We'll filter the "active" status in memory because JSONB querying in LINQ to Entities 
            // without specialized providers/mapping is unreliable across different versions.
            var taxonomyIdsWithListings = await _context.ProductListings
                .Include(l => l.Batch)
                .Where(l => l.Batch != null && l.Batch.TaxonomyId != null)
                .Select(l => new { l.Batch!.TaxonomyId, l.StatusInfo, l.Id, l.BatchId })
                .ToListAsync(cancellationToken);

            // Filter for active/published in memory
            var activeTaxonomyIds = taxonomyIdsWithListings
                .Where(l => {
                    if (l.StatusInfo == null) return false;
                    var status = l.StatusInfo.RootElement.TryGetProperty("status", out var st) ? st.GetString() : null;
                    return status == "active" || status == "published";
                })
                .Select(l => l.TaxonomyId!.Value)
                .Distinct()
                .ToList();

            q = q.Where(t => activeTaxonomyIds.Contains(t.Id));
        }

        var totalCount = await q.CountAsync(cancellationToken);
        var items = await q
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken); 
        
        var pagedItems = items;

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
