using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Domain.Entities;
using MediatR;

namespace decorativeplant_be.Application.Features.IoT.Commands.IngestSensorData;

public class IngestSensorDataCommandHandler : IRequestHandler<IngestSensorDataCommand, bool>
{
    private readonly IIotRepository _iotRepository;
    private readonly IUnitOfWork _unitOfWork;

    public IngestSensorDataCommandHandler(IIotRepository iotRepository, IUnitOfWork unitOfWork)
    {
        _iotRepository = iotRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(IngestSensorDataCommand request, CancellationToken cancellationToken)
    {
        var device = await _iotRepository.GetDeviceBySecretAsync(request.DeviceSecret, cancellationToken);
        if (device == null || device.Status != "Active")
        {
            throw new UnauthorizedAccessException("Invalid or inactive device.");
        }

        var reading = new SensorReading
        {
            Id = Guid.NewGuid(),
            DeviceId = device.Id,
            ComponentKey = request.ComponentKey,
            Value = request.Value,
            RecordedAt = DateTime.UtcNow
        };

        await _iotRepository.AddSensorReadingAsync(reading, cancellationToken);

        // --- Automatic Alert Generation ---
        var rules = await _iotRepository.GetAutomationRulesAsync(device.Id, cancellationToken);
        var activeRules = rules.Where(r => r.IsActive);

        foreach (var rule in activeRules)
        {
            if (rule.Conditions == null) continue;

            try
            {
                var conditions = JsonSerializer.Deserialize<List<JsonElement>>(rule.Conditions.RootElement.GetRawText());
                if (conditions == null) continue;

                foreach (var condition in conditions)
                {
                    var compKey = condition.GetProperty("component_key").GetString();
                    if (compKey != request.ComponentKey) continue;

                    var op = condition.GetProperty("operator").GetString();
                    var thresholdStr = condition.GetProperty("threshold").GetRawText();
                    if (!decimal.TryParse(thresholdStr, out decimal threshold)) continue;

                    bool triggered = op switch
                    {
                        ">" => request.Value > threshold,
                        "<" => request.Value < threshold,
                        "=" => request.Value == threshold,
                        ">=" => request.Value >= threshold,
                        "<=" => request.Value <= threshold,
                        _ => false
                    };

                    if (triggered)
                    {
                        var alert = new IotAlert
                        {
                            Id = Guid.NewGuid(),
                            DeviceId = device.Id,
                            ComponentKey = request.ComponentKey,
                            AlertInfo = JsonSerializer.SerializeToDocument(new
                            {
                                message = $"Threshold Breached: {rule.Name}",
                                ruleId = rule.Id,
                                observedValue = request.Value,
                                threshold = threshold,
                                operatorUsed = op
                            }),
                            CreatedAt = DateTime.UtcNow
                        };
                        await _iotRepository.CreateIotAlertAsync(alert, cancellationToken);
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error but don't fail ingestion
                Console.WriteLine($"Error evaluating automation rule {rule.Id}: {ex.Message}");
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}
