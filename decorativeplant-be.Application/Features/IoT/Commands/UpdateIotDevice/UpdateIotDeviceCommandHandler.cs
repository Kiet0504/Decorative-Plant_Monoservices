using decorativeplant_be.Application.Common.Interfaces;
using MediatR;

namespace decorativeplant_be.Application.Features.IoT.Commands.UpdateIotDevice;

public class UpdateIotDeviceCommandHandler : IRequestHandler<UpdateIotDeviceCommand, bool>
{
    private readonly IIotRepository _iotRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateIotDeviceCommandHandler(IIotRepository iotRepository, IUnitOfWork unitOfWork)
    {
        _iotRepository = iotRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(UpdateIotDeviceCommand request, CancellationToken cancellationToken)
    {
        var device = await _iotRepository.GetIotDeviceByIdAsync(request.Id, cancellationToken);
        if (device == null)
        {
            return false; // Not Found
        }

        device.BranchId = request.Device.BranchId ?? device.BranchId;
        device.LocationId = request.Device.LocationId ?? device.LocationId;
        device.DeviceInfo = request.Device.DeviceInfo ?? device.DeviceInfo;
        device.Status = request.Device.Status ?? device.Status;
        device.Components = request.Device.Components ?? device.Components;

        await _iotRepository.UpdateIotDeviceAsync(device, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}
