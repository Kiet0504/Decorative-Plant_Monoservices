using MediatR;

namespace decorativeplant_be.Application.Features.HealthCheck.Queries;

public class GetHealthSummaryQuery : IRequest<HealthSummaryDto>
{
}

public class HealthSummaryDto
{
    public int TotalBatch { get; set; }
    public int TotalPlant { get; set; }
    public int TotalReportIncidents { get; set; }
    public int Critical { get; set; }
}
