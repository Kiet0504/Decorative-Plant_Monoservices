using MediatR;

namespace decorativeplant_be.Application.Features.IoT.Commands.DeleteIotDevice;

public class DeleteIotDeviceCommand : IRequest<bool>
{
    public Guid Id { get; set; }

    public DeleteIotDeviceCommand(Guid id)
    {
        Id = id;
    }
}
