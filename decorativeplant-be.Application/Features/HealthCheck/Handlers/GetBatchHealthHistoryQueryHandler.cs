using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.HealthCheck.DTOs;
using decorativeplant_be.Application.Features.HealthCheck.Queries;
using decorativeplant_be.Domain.Entities;
using MediatR;

namespace decorativeplant_be.Application.Features.HealthCheck.Handlers;

public class GetBatchHealthHistoryQueryHandler : IRequestHandler<GetBatchHealthHistoryQuery, List<HealthIncidentDto>>
{
    private readonly IRepositoryFactory _repositoryFactory;

    public GetBatchHealthHistoryQueryHandler(IRepositoryFactory repositoryFactory)
    {
        _repositoryFactory = repositoryFactory;
    }

    public async Task<List<HealthIncidentDto>> Handle(GetBatchHealthHistoryQuery request, CancellationToken cancellationToken)
    {
        var repo = _repositoryFactory.CreateRepository<HealthIncident>();
        var incidents = await repo.FindAsync(h => h.BatchId == request.BatchId, cancellationToken);

        // Map to DTO
        var dtos = incidents.Select(HealthIncidentMapper.ToDto).ToList();
        
        return dtos;
    }
}
