using decorativeplant_be.Application.Features.HealthCheck.DTOs;
using MediatR;

namespace decorativeplant_be.Application.Features.HealthCheck.Queries;

public class GetHealthIncidentByIdQuery : IRequest<HealthIncidentDto?>
{
    public Guid Id { get; set; }
}
