using decorativeplant_be.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace decorativeplant_be.Application.Features.IoT.Commands.SendDeviceCommand;

public class SendDeviceCommandCommandHandler : IRequestHandler<SendDeviceCommand, bool>
{
    private readonly IIotRepository _iotRepository;
    private readonly IMqttService _mqttService;
    private readonly ILogger<SendDeviceCommandCommandHandler> _logger;

    public SendDeviceCommandCommandHandler(
        IIotRepository iotRepository,
        IMqttService mqttService,
        ILogger<SendDeviceCommandCommandHandler> logger)
    {
        _iotRepository = iotRepository;
        _mqttService = mqttService;
        _logger = logger;
    }

    public async Task<bool> Handle(SendDeviceCommand request, CancellationToken cancellationToken)
    {
        var device = await _iotRepository.GetIotDeviceByIdAsync(request.DeviceId, cancellationToken);
        if (device == null)
        {
            _logger.LogWarning("Cannot send command to non-existent device: {DeviceId}", request.DeviceId);
            return false;
        }

        if (string.IsNullOrEmpty(device.SecretKey))
        {
            _logger.LogWarning("Device {DeviceId} has no secret key, cannot route MQTT command", request.DeviceId);
            return false;
        }

        try
        {
            await _mqttService.PublishCommandAsync(
                device.SecretKey,
                request.Action,
                new { value = request.Value, params_data = request.Params },
                cancellationToken);

            _logger.LogInformation("Successfully dispatched command '{Action}' to device {DeviceId}", request.Action, request.DeviceId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing MQTT command to device {DeviceId}", request.DeviceId);
            return false;
        }
    }
}
