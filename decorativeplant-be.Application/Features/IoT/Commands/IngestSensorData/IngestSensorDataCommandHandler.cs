using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Domain.Entities;
using MediatR;
using System.Text.Json;

namespace decorativeplant_be.Application.Features.IoT.Commands.IngestSensorData;

public class IngestSensorDataCommandHandler : IRequestHandler<IngestSensorDataCommand, bool>
{
    private readonly IIotRepository _iotRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPublisher _publisher;

    public IngestSensorDataCommandHandler(IIotRepository iotRepository, IUnitOfWork unitOfWork, IPublisher publisher)
    {
        _iotRepository = iotRepository;
        _unitOfWork = unitOfWork;
        _publisher = publisher;
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
        
        // --- Update Activity Log ---
        var activityDict = new Dictionary<string, string>();
        if (device.ActivityLog != null)
        {
            try
            {
                var existing = JsonSerializer.Deserialize<Dictionary<string, string>>(device.ActivityLog.RootElement.GetRawText());
                if (existing != null) activityDict = existing;
            }
            catch { }
        }
        activityDict["last_data_at"] = reading.RecordedAt?.ToString("o") ?? DateTime.UtcNow.ToString("o"); // ISO 8601
        device.ActivityLog = JsonSerializer.SerializeToDocument(activityDict);
        await _iotRepository.UpdateIotDeviceAsync(device, cancellationToken);

        // --- Automatic Alert Generation ---
        var rules = await _iotRepository.GetAutomationRulesAsync(device.Id, null, cancellationToken);
        var activeRules = rules.Where(r => r.IsActive);

        foreach (var rule in activeRules)
        {
            if (rule.Conditions == null) continue;

            try
            {
                var root = rule.Conditions.RootElement;
                List<JsonElement> rulesList = new();

                if (root.ValueKind == JsonValueKind.Array)
                {
                    rulesList = root.EnumerateArray().ToList();
                }
                else if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("rules", out var rulesProp))
                {
                    rulesList = rulesProp.EnumerateArray().ToList();
                }

                if (rulesList.Count == 0) continue;

                foreach (var condition in rulesList)
                {
                    if (!condition.TryGetProperty("component_key", out var compKeyProp)) continue;
                    var compKey = compKeyProp.GetString();
                    if (compKey != request.ComponentKey) continue;

                    if (!condition.TryGetProperty("operator", out var opProp)) continue;
                    var op = opProp.GetString();

                    decimal threshold = 0;
                    if (condition.TryGetProperty("threshold", out var thresholdProp))
                    {
                        if (thresholdProp.ValueKind == JsonValueKind.Number)
                        {
                            threshold = thresholdProp.GetDecimal();
                        }
                        else if (thresholdProp.ValueKind == JsonValueKind.String)
                        {
                            if (!decimal.TryParse(thresholdProp.GetString(), out threshold)) continue;
                        }
                        else
                        {
                            continue;
                        }
                    }

                    bool triggered = op switch
                    {
                        ">" => request.Value > threshold,
                        "<" => request.Value < threshold,
                        "=" => request.Value == threshold,
                        "==" => request.Value == threshold,
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
                                type = "RULE VIOLATION",
                                message = $"Threshold Breached: {rule.Name}",
                                ruleId = rule.Id,
                                observedValue = request.Value,
                                threshold = threshold,
                                operatorUsed = op,
                                sensor = request.ComponentKey,
                                description = $"The sensor reported a value of {request.Value}, which violates the '{rule.Name}' rule ({op} {threshold}).",
                                solution = "Inspect area and adjust environment."
                            }),
                            CreatedAt = DateTime.UtcNow
                        };
                        await _iotRepository.CreateIotAlertAsync(alert, cancellationToken);

                        // Broadcast Domain Event into the background to handle asynchronous tasks like dispatching Emails
                        await _publisher.Publish(new decorativeplant_be.Application.Features.IoT.Events.IotAlertTriggeredNotification
                        {
                            Device = device,
                            Alert = alert,
                            RuleName = rule.Name
                        }, cancellationToken);
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
