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
        var totalBatch = await _context.PlantBatches.CountAsync(cancellationToken);
        var totalPlant = await _context.PlantBatches.SumAsync(b => b.CurrentTotalQuantity ?? 0, cancellationToken);
        var totalReportIncidents = await _context.HealthIncidents.CountAsync(cancellationToken);
        var critical = await _context.HealthIncidents.CountAsync(i => i.Severity == "Critical", cancellationToken);

        return new HealthSummaryDto
        {
            TotalBatch = totalBatch,
            TotalPlant = totalPlant,
            TotalReportIncidents = totalReportIncidents,
            Critical = critical
        };
    }
}
