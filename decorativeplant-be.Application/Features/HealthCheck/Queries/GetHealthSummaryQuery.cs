using MediatR;
using System.Text.Json.Serialization;

namespace decorativeplant_be.Application.Features.HealthCheck.Queries;

public class GetHealthSummaryQuery : IRequest<HealthSummaryDto>
{
    public Guid? BranchId { get; set; }
}

public class HealthSummaryDto
{
    [JsonPropertyName("totalBatch")]
    public int TotalBatch { get; set; }

    [JsonPropertyName("totalPlant")]
    public int TotalPlant { get; set; }

    [JsonPropertyName("totalReportIncidents")]
    public int TotalReportIncidents { get; set; }

    [JsonPropertyName("critical")]
    public int Critical { get; set; }
}
