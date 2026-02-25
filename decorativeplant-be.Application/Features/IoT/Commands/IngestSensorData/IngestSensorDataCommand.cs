using MediatR;

namespace decorativeplant_be.Application.Features.IoT.Commands.IngestSensorData;

public class IngestSensorDataCommand : IRequest<bool>
{
    public string DeviceSecret { get; set; } = string.Empty;
    public string ComponentKey { get; set; } = string.Empty;
    public decimal Value { get; set; }
}
