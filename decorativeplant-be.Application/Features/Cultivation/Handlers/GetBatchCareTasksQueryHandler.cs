using decorativeplant_be.Application.Common.DTOs.Garden;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Cultivation.DTOs;
using decorativeplant_be.Application.Features.Cultivation.Queries;
using decorativeplant_be.Application.Features.Cultivation;
using Microsoft.EntityFrameworkCore;
using MediatR;

using Microsoft.Extensions.Logging;

namespace decorativeplant_be.Application.Features.Cultivation.Handlers;

public class GetBatchCareTasksQueryHandler : IRequestHandler<GetBatchCareTasksQuery, PagedResultDto<BatchCareTaskDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<GetBatchCareTasksQueryHandler> _logger;

    public GetBatchCareTasksQueryHandler(IApplicationDbContext context, ILogger<GetBatchCareTasksQueryHandler> logger)
    {
        _context = context;
        _logger = logger;
    }


    public async Task<PagedResultDto<BatchCareTaskDto>> Handle(GetBatchCareTasksQuery request, CancellationToken cancellationToken)
    {
        var query = _context.CultivationLogs
            .Include(c => c.Batch)
                .ThenInclude(b => b!.Taxonomy)
            .AsNoTracking()
            .AsQueryable();

        // 1. Initial Filtering (Database Level)
        if (!string.IsNullOrEmpty(request.Status) && !request.Status.Equals("All Status", StringComparison.OrdinalIgnoreCase))
        {
            if (request.Status.Equals("Done", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(c => c.PerformedAt != null);
            }
            else
            {
                // For Pending, Warning, High, etc.
                query = query.Where(c => c.PerformedAt == null);
            }
        }

        // 2. Count Total (Database Level)
        var totalCount = await query.CountAsync(cancellationToken);

        // 3. Sorting and Pagination
        if (request.SortOrder?.ToLower() == "asc")
        {
            query = query.OrderBy(c => c.PerformedAt ?? DateTime.MaxValue);
        }
        else
        {
            query = query.OrderByDescending(c => c.PerformedAt ?? DateTime.MaxValue);
        }

        var items = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        // 4. Mapping to DTO
        var dtos = items.Select(CultivationMapper.ToTaskDto).ToList();

        // 5. In-Memory Post filtering (for JSONB properties)
        if (!string.IsNullOrEmpty(request.Status) && 
            !request.Status.Equals("Done", StringComparison.OrdinalIgnoreCase) && 
            !request.Status.Equals("All Status", StringComparison.OrdinalIgnoreCase) &&
            !request.Status.Equals("Pending", StringComparison.OrdinalIgnoreCase))
        {
            dtos = dtos.Where(d => d.Status != null && d.Status.Equals(request.Status, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (!string.IsNullOrEmpty(request.SearchTerm))
        {
            var search = request.SearchTerm.ToLower();
            dtos = dtos.Where(d => 
                (d.ProductName ?? "").ToLower().Contains(search) || 
                (d.Batch ?? "").ToLower().Contains(search) || 
                (d.Activity ?? "").ToLower().Contains(search)
            ).ToList();
        }

        return new PagedResultDto<BatchCareTaskDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }

}



