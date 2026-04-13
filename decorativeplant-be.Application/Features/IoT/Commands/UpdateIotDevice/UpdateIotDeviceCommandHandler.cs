using System.Text.Json;
using decorativeplant_be.Application.Common.Exceptions;
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

        if (!string.IsNullOrWhiteSpace(request.Device.Name))
        {
            var existingDevices = await _iotRepository.GetIotDevicesAsync(cancellationToken);
            foreach (var d in existingDevices.Where(x => x.Id != request.Id))
            {
                if (d.DeviceInfo != null)
                {
                    if (d.DeviceInfo.RootElement.TryGetProperty("name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String)
                    {
                        var existingName = nameProp.GetString();
                        if (string.Equals(existingName, request.Device.Name, StringComparison.OrdinalIgnoreCase))
                        {
                            throw new BadRequestException("Tên thiết bị cảm biến đã tồn tại. Vui lòng chọn một tên khác.");
                        }
                    }
                }
            }
        }

        // 1. Reconstruct DeviceInfo metadata
        var deviceInfoDict = new Dictionary<string, object>();
        
        // Start with existing database state
        if (device.DeviceInfo != null)
        {
            try {
                var existing = JsonSerializer.Deserialize<Dictionary<string, object>>(device.DeviceInfo.RootElement.GetRawText());
                if (existing != null) deviceInfoDict = existing;
            } catch { }
        }

        // Overlay incoming DeviceInfo if provided in the DTO
        if (request.Device.DeviceInfo != null)
        {
            try {
                var incoming = JsonSerializer.Deserialize<Dictionary<string, object>>(request.Device.DeviceInfo.RootElement.GetRawText());
                if (incoming != null)
                {
                    foreach (var kvp in incoming)
                    {
                        deviceInfoDict[kvp.Key] = kvp.Value;
                    }
                }
            } catch { }
        }

        // Roots fields (Name/Type) in the DTO override anything inside DeviceInfo
        if (request.Device.Name != null) deviceInfoDict["name"] = request.Device.Name;
        if (request.Device.Type != null) deviceInfoDict["type"] = request.Device.Type;

        // 2. Update core fields
        // We use request.Device.BranchId if it's provided (not null). 
        // Note: For PATCH, we only update if the property was included. 
        // In simple DTOs, we check if it's not null.
        if (request.Device.BranchId.HasValue) device.BranchId = request.Device.BranchId.Value == Guid.Empty ? null : request.Device.BranchId;
        if (request.Device.LocationId.HasValue) device.LocationId = request.Device.LocationId.Value == Guid.Empty ? null : request.Device.LocationId;
        
        device.DeviceInfo = deviceInfoDict.Count > 0 ? JsonSerializer.SerializeToDocument(deviceInfoDict) : device.DeviceInfo;
        device.Status = request.Device.Status ?? device.Status;
        
        // Handle explicit components update
        if (request.Device.Components != null)
        {
            device.Components = request.Device.Components;
        }

        // Ensure ActivityLog structure is present for legacy devices
        if (device.ActivityLog == null)
        {
            var activityLog = new {
                last_heartbeat_at = (string?)null,
                last_data_at = (string?)null
            };
            device.ActivityLog = JsonSerializer.SerializeToDocument(activityLog);
        }

        await _iotRepository.UpdateIotDeviceAsync(device, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}
