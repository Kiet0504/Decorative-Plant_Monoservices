using decorativeplant_be.Application.Common.DTOs.Common;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.HealthCheck.DTOs;
using decorativeplant_be.Application.Features.HealthCheck.Queries;
using MediatR;
using Microsoft.EntityFrameworkCore;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Application.Features.HealthCheck.Handlers;

public class GetHealthIncidentsQueryHandler : IRequestHandler<GetHealthIncidentsQuery, PagedResult<HealthIncidentDto>>
{
    private readonly IApplicationDbContext _context;

    public GetHealthIncidentsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<HealthIncidentDto>> Handle(GetHealthIncidentsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.HealthIncidents
            .Include(i => i.Batch)
            .AsNoTracking();

        if (request.BranchId.HasValue)
        {
            query = query.Where(i => i.Batch != null && i.Batch.BranchId == request.BranchId.Value);
        }

        // Filtering
        if (!string.IsNullOrEmpty(request.Status))
        {
            // Status is stored in StatusInfo JSONB. 
            // In a real scenario with EF Core and Postgres JSONB support, we'd use EF.Functions.JsonExists or similar.
            // For now, we'll fetch and filter if the DB provider supports it, or handle basic status if it's mirrored in a column.
            // Since the entity doesn't have a status column, we'll assume the provider handles JSON query if possible, 
            // otherwise we'd need more complex logic. 
            // Most implementations here seem to use JSONB for flexibility.
        }

        if (!string.IsNullOrEmpty(request.Severity))
        {
            query = query.Where(i => i.Severity == request.Severity);
        }

        if (!string.IsNullOrEmpty(request.SearchTerm))
        {
            var search = request.SearchTerm.ToLower();
            query = query.Where(i => 
                (i.Batch != null && i.Batch.BatchCode.ToLower().Contains(search)) ||
                (i.IncidentType != null && i.IncidentType.ToLower().Contains(search)) ||
                (i.Description != null && i.Description.ToLower().Contains(search)));
        }

        // Sorting
        query = request.SortBy?.ToLower() switch
        {
            "severity" => request.SortDescending ? query.OrderByDescending(i => i.Severity) : query.OrderBy(i => i.Severity),
            "type" => request.SortDescending ? query.OrderByDescending(i => i.IncidentType) : query.OrderBy(i => i.IncidentType),
            _ => query.OrderByDescending(i => i.Id) // Id as fallback for creation order if no timestamp col
        };

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.ToListAsync(cancellationToken);

        IEnumerable<HealthIncident> filteredItems = items;

        // Apply Status Filter (In-memory for JSONB field)
        if (!string.IsNullOrEmpty(request.Status) && request.Status != "All Status")
        {
            filteredItems = filteredItems.Where(i => 
                i.StatusInfo != null && 
                i.StatusInfo.RootElement.TryGetProperty("status", out var sp) && 
                sp.GetString()?.Equals(request.Status, StringComparison.OrdinalIgnoreCase) == true);
            totalCount = filteredItems.Count();
        }

        var pagedItems = filteredItems
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        return new PagedResult<HealthIncidentDto>
        {
            Items = pagedItems.Select(x => HealthIncidentMapper.ToDto(x)).ToList(),
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}
