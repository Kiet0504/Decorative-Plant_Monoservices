using System.Text.Json;
using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.DTOs.IoT;
using decorativeplant_be.Domain.Entities;
using MediatR;

namespace decorativeplant_be.Application.Features.IoT.Commands.CreateIotDevice;

public class CreateIotDeviceCommandHandler : IRequestHandler<CreateIotDeviceCommand, IotDeviceDto>
{
    private readonly IIotRepository _iotRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateIotDeviceCommandHandler(IIotRepository iotRepository, IUnitOfWork unitOfWork)
    {
        _iotRepository = iotRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<IotDeviceDto> Handle(CreateIotDeviceCommand request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.Device.Name))
        {
            var existingDevices = await _iotRepository.GetIotDevicesAsync(cancellationToken);
            foreach (var d in existingDevices)
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

        var generatedSecret = Guid.NewGuid().ToString("N");

        var deviceInfoDict = new Dictionary<string, object>();
        if (request.Device.DeviceInfo != null)
        {
            try {
                var existing = JsonSerializer.Deserialize<Dictionary<string, object>>(request.Device.DeviceInfo.RootElement.GetRawText());
                if (existing != null) deviceInfoDict = existing;
            } catch { }
        }
        
        if (!string.IsNullOrEmpty(request.Device.Name)) deviceInfoDict["name"] = request.Device.Name;
        if (!string.IsNullOrEmpty(request.Device.Type)) deviceInfoDict["type"] = request.Device.Type;

        var newDevice = new IotDevice
        {
            Id = Guid.NewGuid(),
            BranchId = request.Device.BranchId,
            LocationId = request.Device.LocationId,
            SecretKey = generatedSecret,
            DeviceInfo = deviceInfoDict.Count > 0 ? JsonSerializer.SerializeToDocument(deviceInfoDict) : request.Device.DeviceInfo,
            Status = request.Device.Status ?? "Active",
            Components = request.Device.Components
        };

        await _iotRepository.CreateIotDeviceAsync(newDevice, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new IotDeviceDto
        {
            Id = newDevice.Id,
            BranchId = newDevice.BranchId,
            LocationId = newDevice.LocationId,
            SecretKey = newDevice.SecretKey,
            DeviceInfo = newDevice.DeviceInfo,
            Name = request.Device.Name,
            Type = request.Device.Type,
            Status = newDevice.Status,
            Components = newDevice.Components
        };
    }
}
