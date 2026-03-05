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

        return new IotDeviceDto
        {
            Id = device.Id,
            BranchId = device.BranchId,
            LocationId = device.LocationId,
            SecretKey = device.SecretKey,
            DeviceInfo = device.DeviceInfo,
            Status = device.Status,
            ActivityLog = device.ActivityLog,
            Components = device.Components
        };
    }
}
