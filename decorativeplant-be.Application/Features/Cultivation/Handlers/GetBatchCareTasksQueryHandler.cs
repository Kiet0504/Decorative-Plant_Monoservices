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
            .Include(c => c.Batch)
                .ThenInclude(b => b!.Branch)
            .AsNoTracking()
            .AsQueryable();
            
        if (request.BranchId.HasValue)
        {
            query = query.Where(c => c.Batch != null && c.Batch.BranchId == request.BranchId.Value);
        }

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

        // Fetch all items into memory since we need complex deduplication
        var items = await query.ToListAsync(cancellationToken);

        // Map to DTO
        var dtos = items.Select(CultivationMapper.ToTaskDto).ToList();

        // Deduplicate identical tasks in same batch (Rule: prioritize pending, otherwise newest done)
        var deduplicatedDtos = new List<BatchCareTaskDto>();
        foreach (var group in dtos.GroupBy(d => new { d.Batch, d.Activity }))
        {
            var tasksInGroup = group.ToList();
            
            // Sort by date descending
            tasksInGroup = tasksInGroup.OrderByDescending(t => 
                DateTime.TryParse(t.Date, out var date) ? date : DateTime.MinValue).ToList();

            var pendingTask = tasksInGroup.FirstOrDefault(t => !t.Status.Equals("Done", StringComparison.OrdinalIgnoreCase));
            
            if (pendingTask != null)
            {
                deduplicatedDtos.Add(pendingTask);
            }
            else
            {
                deduplicatedDtos.Add(tasksInGroup.First());
            }
        }

        dtos = deduplicatedDtos;

        // In-Memory Post filtering
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

        var totalCount = dtos.Count;

        // Apply pagination
        var pagedDtos = dtos
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        return new PagedResultDto<BatchCareTaskDto>
        {
            Items = pagedDtos,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }

}



