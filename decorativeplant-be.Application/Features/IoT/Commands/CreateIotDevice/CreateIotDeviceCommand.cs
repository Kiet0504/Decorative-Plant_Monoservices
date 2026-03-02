using decorativeplant_be.Application.DTOs.IoT;
using MediatR;

namespace decorativeplant_be.Application.Features.IoT.Commands.CreateIotDevice;

public class CreateIotDeviceCommand : IRequest<IotDeviceDto>
{
    public CreateIotDeviceDto Device { get; set; } = null!;
}
