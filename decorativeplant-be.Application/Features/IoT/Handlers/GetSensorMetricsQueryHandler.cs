using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.DTOs.IoT;
using decorativeplant_be.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace decorativeplant_be.Application.Features.IoT.Handlers;

public class GetSensorMetricsQueryHandler : IRequestHandler<Queries.GetSensorMetricsQuery, IEnumerable<SensorReadingDto>>
{
    private readonly IRepositoryFactory _repositoryFactory;

    public GetSensorMetricsQueryHandler(IRepositoryFactory repositoryFactory)
    {
        _repositoryFactory = repositoryFactory;
    }

    public async Task<IEnumerable<SensorReadingDto>> Handle(Queries.GetSensorMetricsQuery request, CancellationToken cancellationToken)
    {
        var repo = _repositoryFactory.CreateRepository<SensorReading>();
        
        var query = await repo.GetAllAsync(cancellationToken);
        
        var queryable = query.AsQueryable()
            .Where(x => x.DeviceId == request.DeviceId);

        if (!string.IsNullOrEmpty(request.ComponentKey))
        {
            queryable = queryable.Where(x => x.ComponentKey == request.ComponentKey);
        }

        if (request.StartTime.HasValue)
        {
            queryable = queryable.Where(x => x.RecordedAt >= request.StartTime.Value);
        }

        if (request.EndTime.HasValue)
        {
            queryable = queryable.Where(x => x.RecordedAt <= request.EndTime.Value);
        }

        var results = queryable
            .OrderByDescending(x => x.RecordedAt)
            .Take(1000) // limit to avoid massive payloads
            .Select(x => new SensorReadingDto
            {
                Id = x.Id,
                DeviceId = x.DeviceId,
                ComponentKey = x.ComponentKey ?? string.Empty,
                Value = x.Value,
                Timestamp = x.RecordedAt ?? DateTime.UtcNow
            })
            .ToList();

        return results;
    }
}
