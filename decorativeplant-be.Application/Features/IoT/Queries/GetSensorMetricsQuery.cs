using decorativeplant_be.Application.DTOs.IoT;
using MediatR;

namespace decorativeplant_be.Application.Features.IoT.Queries;

public class GetSensorMetricsQuery : IRequest<IEnumerable<SensorReadingDto>>
{
    public Guid DeviceId { get; set; }
    public string? ComponentKey { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
}
