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
        bool? masterAutomationStatus = null;
        
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
                var root = request.Device.DeviceInfo.RootElement;
                
                // Track if master toggle is being changed (Check both casings)
                JsonElement autoProp = default;
                bool found = root.TryGetProperty("isAutomationEnabled", out autoProp) || 
                             root.TryGetProperty("IsAutomationEnabled", out autoProp);

                if (found)
                {
                    if (autoProp.ValueKind == JsonValueKind.True) masterAutomationStatus = true;
                    else if (autoProp.ValueKind == JsonValueKind.False) masterAutomationStatus = false;
                    else if (autoProp.ValueKind == JsonValueKind.String)
                    {
                        masterAutomationStatus = !string.Equals(autoProp.GetString(), "false", StringComparison.OrdinalIgnoreCase);
                    }
                }

                var incoming = JsonSerializer.Deserialize<Dictionary<string, object>>(root.GetRawText());
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
        if (request.Device.BranchId.HasValue) device.BranchId = request.Device.BranchId.Value == Guid.Empty ? null : request.Device.BranchId;
        if (request.Device.LocationId.HasValue) device.LocationId = request.Device.LocationId.Value == Guid.Empty ? null : request.Device.LocationId;

        device.DeviceInfo = deviceInfoDict.Count > 0 ? JsonSerializer.SerializeToDocument(deviceInfoDict) : device.DeviceInfo;
        device.Status = request.Device.Status ?? device.Status;

        // 3. Synchronize Automation Rules if master toggle changed
        if (masterAutomationStatus.HasValue && device.AutomationRules != null)
        {
            foreach (var rule in device.AutomationRules)
            {
                rule.IsActive = masterAutomationStatus.Value;
                // No need to call UpdateAsync explicitly as EF tracks these included entities
            }
        }
        
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
