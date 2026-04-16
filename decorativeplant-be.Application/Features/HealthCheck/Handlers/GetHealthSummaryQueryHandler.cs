using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.HealthCheck.Queries;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace decorativeplant_be.Application.Features.HealthCheck.Handlers;

public class GetHealthSummaryQueryHandler : IRequestHandler<GetHealthSummaryQuery, HealthSummaryDto>
{
    private readonly IApplicationDbContext _context;

    public GetHealthSummaryQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<HealthSummaryDto> Handle(GetHealthSummaryQuery request, CancellationToken cancellationToken)
    {
        var batchQuery = _context.PlantBatches.AsNoTracking();
        var incidentQuery = _context.HealthIncidents.AsNoTracking();

        if (request.BranchId.HasValue)
        {
            batchQuery = batchQuery.Where(b => b.BranchId == request.BranchId.Value);
            incidentQuery = incidentQuery.Where(i => i.Batch != null && i.Batch.BranchId == request.BranchId.Value);
        }

        var totalBatch = await batchQuery.CountAsync(cancellationToken);
        var totalPlant = await batchQuery.SumAsync(b => b.CurrentTotalQuantity ?? 0, cancellationToken);
        var totalReportIncidents = await incidentQuery.CountAsync(cancellationToken);
        var critical = await incidentQuery.CountAsync(i => i.Severity == "Critical", cancellationToken);

        return new HealthSummaryDto
        {
            TotalBatch = totalBatch,
            TotalPlant = totalPlant,
            TotalReportIncidents = totalReportIncidents,
            Critical = critical
        };
    }
}
