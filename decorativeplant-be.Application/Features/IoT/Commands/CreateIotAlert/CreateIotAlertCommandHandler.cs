using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.DTOs.IoT;
using decorativeplant_be.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using MediatR;

namespace decorativeplant_be.Application.Features.IoT.Commands.CreateIotAlert;

public class CreateIotAlertCommandHandler : IRequestHandler<CreateIotAlertCommand, IotAlertDto>
{
    private readonly IIotRepository _iotRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IApplicationDbContext _context;
    private readonly IPublisher _publisher;

    public CreateIotAlertCommandHandler(
        IIotRepository iotRepository, 
        IUnitOfWork unitOfWork,
        IApplicationDbContext context,
        IPublisher publisher)
    {
        _iotRepository = iotRepository;
        _unitOfWork = unitOfWork;
        _context = context;
        _publisher = publisher;
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

        // --- Trigger Notification (Email, etc.) ---
        try
        {
            var device = await _context.IotDevices
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == request.DeviceId, cancellationToken);

            if (device != null)
            {
                await _publisher.Publish(new decorativeplant_be.Application.Features.IoT.Events.IotAlertTriggeredNotification
                {
                    Device = device,
                    Alert = newAlert,
                    RuleName = "Manual/System Alert"
                }, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Notification Error] Failed to publish IotAlert notification: {ex.Message}");
        }

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
