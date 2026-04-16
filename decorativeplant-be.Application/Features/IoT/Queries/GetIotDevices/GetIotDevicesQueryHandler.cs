using System.Text.Json;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.DTOs.IoT;
using MediatR;

namespace decorativeplant_be.Application.Features.IoT.Queries.GetIotDevices;

public class GetIotDevicesQueryHandler : IRequestHandler<GetIotDevicesQuery, IEnumerable<IotDeviceDto>>
{
    private readonly IIotRepository _iotRepository;

    public GetIotDevicesQueryHandler(IIotRepository iotRepository)
    {
        _iotRepository = iotRepository;
    }

    public async Task<IEnumerable<IotDeviceDto>> Handle(GetIotDevicesQuery request, CancellationToken cancellationToken)
    {
        var devices = await _iotRepository.GetIotDevicesAsync(cancellationToken);
        
        if (request.BranchId.HasValue)
        {
            devices = devices.Where(d => d.BranchId == request.BranchId.Value).ToList();
        }
        
        return devices.Select(d => new IotDeviceDto
        {
            Id = d.Id,
            BranchId = d.BranchId,
            LocationId = d.LocationId,
            SecretKey = d.SecretKey,
            DeviceInfo = d.DeviceInfo,
            Name = ExtractJsonField(d.DeviceInfo, "name"),
            Type = ExtractJsonField(d.DeviceInfo, "type"),
            LocationName = d.Location?.Name,
            Status = d.Status,
            BranchName = d.Branch?.Name,
            ActivityLog = d.ActivityLog,
            Components = d.Components
        }).ToList();
    }

    private string? ExtractJsonField(JsonDocument? doc, string fieldName) {
        if (doc == null) return null;
        if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty(fieldName, out var prop)) 
            return prop.GetString();
        return null;
    }
}
