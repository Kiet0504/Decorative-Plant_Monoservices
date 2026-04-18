using MediatR;

namespace decorativeplant_be.Application.Features.IoT.Commands.SendDeviceCommand;

public class SendDeviceCommand : IRequest<bool>
{
    public Guid DeviceId { get; set; }
    public string Action { get; set; } = string.Empty;
    public object? Value { get; set; }
    public object? Params { get; set; }
}
