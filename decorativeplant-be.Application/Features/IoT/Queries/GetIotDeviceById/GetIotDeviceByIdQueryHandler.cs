using System.Text.Json;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.DTOs.IoT;
using MediatR;

namespace decorativeplant_be.Application.Features.IoT.Queries.GetIotDeviceById;

public class GetIotDeviceByIdQueryHandler : IRequestHandler<GetIotDeviceByIdQuery, IotDeviceDto?>
{
    private readonly IIotRepository _iotRepository;

    public GetIotDeviceByIdQueryHandler(IIotRepository iotRepository)
    {
        _iotRepository = iotRepository;
    }

    public async Task<IotDeviceDto?> Handle(GetIotDeviceByIdQuery request, CancellationToken cancellationToken)
    {
        var device = await _iotRepository.GetIotDeviceByIdAsync(request.Id, cancellationToken);
        
        if (device == null)
            return null;

        var readings = await _iotRepository.GetSensorMetricsAsync(device.Id, null, DateTime.UtcNow.AddDays(-7), null, cancellationToken);

        string? ExtractJsonField(JsonDocument? doc, string fieldName) {
            if (doc == null || doc.RootElement.ValueKind != JsonValueKind.Object) return null;
            if (doc.RootElement.TryGetProperty(fieldName, out var prop)) return prop.GetString();
            return null;
        }

        bool isAutomationEnabled = true;
        if (device.DeviceInfo != null)
        {
            try
            {
                if (device.DeviceInfo.RootElement.TryGetProperty("isAutomationEnabled", out var autoProp))
                {
                    isAutomationEnabled = autoProp.ValueKind == JsonValueKind.True || 
                                          (autoProp.ValueKind == JsonValueKind.False ? false : true);
                    
                    if (autoProp.ValueKind == JsonValueKind.String)
                    {
                        isAutomationEnabled = !string.Equals(autoProp.GetString(), "false", StringComparison.OrdinalIgnoreCase);
                    }
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
            Name = ExtractJsonField(device.DeviceInfo, "name"),
            Type = ExtractJsonField(device.DeviceInfo, "type"),
            LocationName = device.Location?.Name,
            Status = device.Status,
            ActivityLog = device.ActivityLog,
            Components = device.Components,
            IsAutomationEnabled = isAutomationEnabled,
            AutomationRules = (device.AutomationRules ?? new List<decorativeplant_be.Domain.Entities.AutomationRule>()).Select(r => new AutomationRuleDto
            {
                Id = r.Id,
                DeviceId = r.DeviceId,
                Name = r.Name,
                Priority = r.Priority,
                IsActive = r.IsActive,
                Schedule = r.Schedule,
                Conditions = r.Conditions,
                Actions = r.Actions,
                BranchId = device.BranchId,
                CreatedAt = r.CreatedAt
            }),
            LatestReadings = readings.Select(r => new SensorReadingDto
            {
                Id = r.Id,
                DeviceId = r.DeviceId,
                ComponentKey = r.ComponentKey ?? "unknown",
                Value = r.Value,
                Timestamp = r.RecordedAt ?? DateTime.UtcNow
            })
        };
    }
}
