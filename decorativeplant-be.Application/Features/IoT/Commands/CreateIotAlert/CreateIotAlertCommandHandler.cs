using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.DTOs.IoT;
using decorativeplant_be.Domain.Entities;
using MediatR;

namespace decorativeplant_be.Application.Features.IoT.Commands.CreateIotAlert;

public class CreateIotAlertCommandHandler : IRequestHandler<CreateIotAlertCommand, IotAlertDto>
{
    private readonly IIotRepository _iotRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateIotAlertCommandHandler(IIotRepository iotRepository, IUnitOfWork unitOfWork)
    {
        _iotRepository = iotRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<IotAlertDto> Handle(CreateIotAlertCommand request, CancellationToken cancellationToken)
    {
        var newAlert = new IotAlert
        {
            Id = Guid.NewGuid(),
            DeviceId = request.DeviceId,
            ComponentKey = request.ComponentKey,
            AlertInfo = request.AlertInfo,
            CreatedAt = DateTime.UtcNow
        };

        await _iotRepository.CreateIotAlertAsync(newAlert, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new IotAlertDto
        {
            Id = newAlert.Id,
            DeviceId = newAlert.DeviceId,
            ComponentKey = newAlert.ComponentKey,
            AlertInfo = newAlert.AlertInfo,
            CreatedAt = newAlert.CreatedAt
        };
    }
}
