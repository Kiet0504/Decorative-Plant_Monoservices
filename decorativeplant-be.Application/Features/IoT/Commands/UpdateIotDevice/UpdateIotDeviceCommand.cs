using decorativeplant_be.Application.DTOs.IoT;
using MediatR;

namespace decorativeplant_be.Application.Features.IoT.Commands.UpdateIotDevice;

public class UpdateIotDeviceCommand : IRequest<bool>
{
    public Guid Id { get; set; }
    public UpdateIotDeviceDto Device { get; set; } = null!;
}
