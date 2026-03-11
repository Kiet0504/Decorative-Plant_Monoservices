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
        // Generate a random, cryptographically secure string, or just a Guid string for the secret
        var generatedSecret = Guid.NewGuid().ToString("N");

        var newDevice = new IotDevice
        {
            Id = Guid.NewGuid(),
            BranchId = request.Device.BranchId,
            LocationId = request.Device.LocationId,
            SecretKey = generatedSecret, // Auto-assigned by backend
            DeviceInfo = request.Device.DeviceInfo,
            Status = request.Device.Status ?? "Inactive",
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
            Status = newDevice.Status,
            Components = newDevice.Components
        };
    }
}
