using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.HealthCheck.DTOs;
using decorativeplant_be.Application.Features.HealthCheck.Queries;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace decorativeplant_be.Application.Features.HealthCheck.Handlers;

public class GetHealthIncidentByIdQueryHandler : IRequestHandler<GetHealthIncidentByIdQuery, HealthIncidentDto?>
{
    private readonly IApplicationDbContext _context;

    public GetHealthIncidentByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<HealthIncidentDto?> Handle(GetHealthIncidentByIdQuery request, CancellationToken cancellationToken)
    {
        var incident = await _context.HealthIncidents
            .Include(i => i.Batch)
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == request.Id, cancellationToken);

        if (incident == null) return null;

        return HealthIncidentMapper.ToDto(incident);
    }
}
