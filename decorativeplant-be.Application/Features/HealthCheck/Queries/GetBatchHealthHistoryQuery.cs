using decorativeplant_be.Application.Features.HealthCheck.DTOs;
using MediatR;

namespace decorativeplant_be.Application.Features.HealthCheck.Queries;

public class GetBatchHealthHistoryQuery : IRequest<List<HealthIncidentDto>>
{
    public Guid BatchId { get; set; }
}
