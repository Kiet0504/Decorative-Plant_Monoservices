using System.Text.Json;
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

        // Pack Name and Type into DeviceInfo
        var deviceInfoDict = new Dictionary<string, object>();
        if (device.DeviceInfo != null)
        {
            try {
                var existing = JsonSerializer.Deserialize<Dictionary<string, object>>(device.DeviceInfo.RootElement.GetRawText());
                if (existing != null) deviceInfoDict = existing;
            } catch { }
        }

        if (request.Device.Name != null) deviceInfoDict["name"] = request.Device.Name;
        if (request.Device.Type != null) deviceInfoDict["type"] = request.Device.Type;

        device.BranchId = request.Device.BranchId ?? device.BranchId;
        device.LocationId = request.Device.LocationId ?? device.LocationId;
        device.DeviceInfo = deviceInfoDict.Count > 0 ? JsonSerializer.SerializeToDocument(deviceInfoDict) : device.DeviceInfo;
        device.Status = request.Device.Status ?? device.Status;
        device.Components = request.Device.Components ?? device.Components;

        await _iotRepository.UpdateIotDeviceAsync(device, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}
