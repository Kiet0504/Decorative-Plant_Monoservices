using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Cultivation.DTOs;
using decorativeplant_be.Application.Features.Cultivation.Queries;
using Microsoft.EntityFrameworkCore;
using MediatR;

namespace decorativeplant_be.Application.Features.Cultivation.Handlers;

public class GetBatchCareTasksSummaryQueryHandler : IRequestHandler<GetBatchCareTasksSummaryQuery, BatchCareTasksSummary>
{
    private readonly IApplicationDbContext _context;

    public GetBatchCareTasksSummaryQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<BatchCareTasksSummary> Handle(GetBatchCareTasksSummaryQuery request, CancellationToken cancellationToken)
    {
        var pendingTasks = await _context.CultivationLogs
            .Where(c => c.PerformedAt == null)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return new BatchCareTasksSummary
        {
            TodayTask = pendingTasks.Count,
            Watering = pendingTasks.Count(t => t.ActivityType != null && t.ActivityType.Contains("Watering", StringComparison.OrdinalIgnoreCase)),
            Fertilizing = pendingTasks.Count(t => t.ActivityType != null && t.ActivityType.Contains("Fertilizing", StringComparison.OrdinalIgnoreCase)),
            PruningRepotting = pendingTasks.Count(t => 
                t.ActivityType != null && 
                (t.ActivityType.Contains("Pruning", StringComparison.OrdinalIgnoreCase) || 
                 t.ActivityType.Contains("Repotting", StringComparison.OrdinalIgnoreCase)))
        };
    }
}
