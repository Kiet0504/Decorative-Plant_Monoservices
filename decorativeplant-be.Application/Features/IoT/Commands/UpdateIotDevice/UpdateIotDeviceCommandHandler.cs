using System.Text.Json;
using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.DTOs.IoT;
using MediatR;

namespace decorativeplant_be.Application.Features.IoT.Commands.UpdateIotDevice;

using decorativeplant_be.Application.Features.IoT.Commands;

public class UpdateIotDeviceCommandHandler : IRequestHandler<UpdateIotDeviceCommand, IotDeviceDto?>
{
    private readonly IIotRepository _iotRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMqttService _mqttService;

    public UpdateIotDeviceCommandHandler(IIotRepository iotRepository, IUnitOfWork unitOfWork, IMqttService mqttService)
    {
        _iotRepository = iotRepository;
        _unitOfWork = unitOfWork;
        _mqttService = mqttService;
    }

    public async Task<IotDeviceDto?> Handle(UpdateIotDeviceCommand request, CancellationToken cancellationToken)
    {
        var device = await _iotRepository.GetIotDeviceByIdAsync(request.Id, cancellationToken);
        if (device == null)
        {
            return null; // Not Found
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

        // Notify device of operating context changes (Season/Stage/Automation Toggle)
        await AutomationRuleMqttNotifier.NotifyDeviceAsync(device.Id, _iotRepository, _mqttService, cancellationToken);

        // Map to DTO for the frontend
        bool isAutomationEnabled = true;
        if (device.DeviceInfo != null)
        {
            try
            {
                if (device.DeviceInfo.RootElement.TryGetProperty("isAutomationEnabled", out var autoProp))
                {
                    isAutomationEnabled = autoProp.ValueKind == JsonValueKind.True;
                    if (autoProp.ValueKind == JsonValueKind.String)
                        isAutomationEnabled = !string.Equals(autoProp.GetString(), "false", StringComparison.OrdinalIgnoreCase);
                }
            }
            catch { }
        }

        return new IotDeviceDto
        {
            Id = device.Id,
            BranchId = device.BranchId,
            LocationId = device.LocationId,
            SecretKey = device.SecretKey,
            DeviceInfo = device.DeviceInfo,
            Name = device.DeviceInfo?.RootElement.TryGetProperty("name", out var n) == true ? n.GetString() : null,
            Type = device.DeviceInfo?.RootElement.TryGetProperty("type", out var t) == true ? t.GetString() : null,
            Status = device.Status,
            ActivityLog = device.ActivityLog,
            Components = device.Components,
            IsAutomationEnabled = isAutomationEnabled,
            AutomationRules = device.AutomationRules?.Select(r => new AutomationRuleDto
            {
                Id = r.Id, DeviceId = r.DeviceId, Name = r.Name, Priority = r.Priority, IsActive = r.IsActive, 
                Schedule = r.Schedule, Conditions = r.Conditions, Actions = r.Actions, CreatedAt = r.CreatedAt
            })
        };
    }
}
