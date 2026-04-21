using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Domain.Entities;
using MediatR;
using System.Text.Json;
using System.Linq;

namespace decorativeplant_be.Application.Features.IoT.Commands.IngestSensorData;

public class IngestSensorDataCommandHandler : IRequestHandler<IngestSensorDataCommand, bool>
{
    private readonly IIotRepository _iotRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPublisher _publisher;
    private readonly IMqttService _mqttService;

    public IngestSensorDataCommandHandler(
        IIotRepository iotRepository, 
        IUnitOfWork unitOfWork, 
        IPublisher publisher,
        IMqttService mqttService)
    {
        _iotRepository = iotRepository;
        _unitOfWork = unitOfWork;
        _publisher = publisher;
        _mqttService = mqttService;
    }

    public async Task<bool> Handle(IngestSensorDataCommand request, CancellationToken cancellationToken)
    {
        // 1. Validate & Update Heartbeat
        var device = await _iotRepository.GetDeviceBySecretAsync(request.DeviceSecret, cancellationToken);
        if (device == null || device.Status != "Active")
        {
            throw new UnauthorizedAccessException("Invalid or inactive device.");
        }

        // --- RECORD HEARTBEAT ---
        try
        {
            var nowStr = DateTime.UtcNow.ToString("o");
            var infoDict = new Dictionary<string, object>();
            if (device.DeviceInfo != null)
            {
                infoDict = JsonSerializer.Deserialize<Dictionary<string, object>>(device.DeviceInfo.RootElement.GetRawText()) ?? new();
            }
            infoDict["lastSeenAt"] = nowStr;
            device.DeviceInfo = JsonSerializer.SerializeToDocument(infoDict);
            await _iotRepository.UpdateIotDeviceAsync(device, cancellationToken);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Heartbeat Error] Failed to update LastSeenAt for device {device.Id}: {ex.Message}");
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

        // --- Automatic Alert & Conflict Detection ---
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

        if (!isAutomationEnabled)
        {
            Console.WriteLine($"[Diagnostic] Automation is DISABLED globally for device {device.Id}. Skipping rules.");
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return true;
        }

        var rules = await _iotRepository.GetAutomationRulesAsync(device.Id, null, cancellationToken);
        var activeRules = rules.Where(r => r.IsActive).ToList();

        // Dictionary to track which actuators are being triggered by which rules
        // Key: Actuator Key, Value: List of (Rule, ActionType)
        var triggeredActuators = new Dictionary<string, List<(AutomationRule Rule, string ActionType)>>();

        foreach (var rule in activeRules)
        {
            if (rule.Conditions == null || rule.Actions == null) continue;

            try
            {
                var condRoot = rule.Conditions.RootElement;
                var logic = "and";
                if (condRoot.ValueKind == JsonValueKind.Object && condRoot.TryGetProperty("logic", out var logicProp))
                {
                    logic = logicProp.GetString()?.ToLower() ?? "and";
                }

                List<JsonElement> rulesList = new();
                if (condRoot.ValueKind == JsonValueKind.Array) rulesList = condRoot.EnumerateArray().ToList();
                else if (condRoot.ValueKind == JsonValueKind.Object && condRoot.TryGetProperty("rules", out var rulesP)) rulesList = rulesP.EnumerateArray().ToList();

                if (rulesList.Count == 0) continue;

                // Simple check for if THIS sensor reading affects this rule
                var isAffectedByThisSensor = rulesList.Any(c => c.TryGetProperty("component_key", out var k) && k.GetString() == request.ComponentKey);
                if (!isAffectedByThisSensor) continue;

                // Evaluate conditions
                bool ruleMatched = (logic == "and");
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
                        if (thresholdProp.ValueKind == JsonValueKind.Number) threshold = thresholdProp.GetDecimal();
                        else if (thresholdProp.ValueKind == JsonValueKind.String)
                        {
                            var sVal = thresholdProp.GetString();
                            if (decimal.TryParse(sVal, out var d)) threshold = d;
                            else if (sVal?.Contains("-") == true)
                            {
                                // Range check
                                var parts = sVal.Split("-");
                                if (parts.Length == 2 && decimal.TryParse(parts[0], out var min) && decimal.TryParse(parts[1], out var max))
                                {
                                    bool inRange = request.Value >= min && request.Value <= max;
                                    bool condTrue = op == "between" ? inRange : !inRange;
                                    ruleMatched = (logic == "and") ? (ruleMatched && condTrue) : (ruleMatched || condTrue);
                                    continue;
                                }
                            }
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

                    ruleMatched = (logic == "and") ? (ruleMatched && triggered) : (ruleMatched || triggered);
                }

                if (ruleMatched)
                {
                    Console.WriteLine($"[Diagnostic] Rule '{rule.Name}' (P{rule.Priority}) MATCHED conditions.");
                    
                    // Identify which actuators this rule wants to control
                    var actionsArray = rule.Actions.RootElement.ValueKind == JsonValueKind.Array 
                        ? rule.Actions.RootElement.EnumerateArray() 
                        : (rule.Actions.RootElement.TryGetProperty("actions", out var ap) ? ap.EnumerateArray() : Enumerable.Empty<JsonElement>());

                    foreach (var action in actionsArray)
                    {
                        var actuatorKey = action.TryGetProperty("target_component_key", out var ak) ? ak.GetString() : (action.TryGetProperty("component", out var c) ? c.GetString() : null);
                        var actionType = action.TryGetProperty("action_type", out var at) ? at.GetString() : (action.TryGetProperty("command", out var cmd) ? cmd.GetString() : "turn_on");

                        if (!string.IsNullOrEmpty(actuatorKey))
                        {
                            Console.WriteLine($"  -> Targets Actuator: '{actuatorKey}' with Action: '{actionType}'");
                            if (!triggeredActuators.ContainsKey(actuatorKey)) triggeredActuators[actuatorKey] = new();
                            triggeredActuators[actuatorKey].Add((rule, actionType ?? "turn_on"));
                        }
                    }
                }
            }
            catch (Exception ex) { Console.WriteLine($"[Diagnostic] Error evaluating rule {rule.Name}: {ex.Message}"); }
        }

        Console.WriteLine($"[Diagnostic] Total Triggered Actuators count: {triggeredActuators.Count}");

        // --- Conflict Resolution Logic ---
        foreach (var actuator in triggeredActuators)
        {
            var actuatorKey = actuator.Key;
            var matchedRules = actuator.Value;

            Console.WriteLine($"[Diagnostic] Checking conflicts for '{actuatorKey}'. Rule count: {matchedRules.Count}");

            if (matchedRules.Count > 1)
            {
                // Conflict confirmed
                var p1Rules = matchedRules.Where(r => r.Rule.Priority == 1).ToList();
                var hasActionMismatch = matchedRules.Select(r => r.ActionType).Distinct().Count() > 1;

                Console.WriteLine($"[Diagnostic] Conflict Stats - P1 Count: {p1Rules.Count}, Action Mismatch: {hasActionMismatch}");

                if (p1Rules.Count > 1 || hasActionMismatch)
                {
                    var ruleNames = string.Join(", ", matchedRules.Select(r => $"'{r.Rule.Name}'"));
                    
                    // 1. Send STOP Command via MQTT (Kill-switch)
                    // We send "value: turn_off" so main.py on ESP32 can parse it correctly
                    await _mqttService.PublishCommandAsync(device.SecretKey, actuatorKey, new { value = "turn_off", note = "Emergency stop due to rule conflict" }, cancellationToken);

                    // --- Updated Alert Deduplication & Email Throttling Logic ---
                    var existingAlerts = await _iotRepository.GetIotAlertsAsync(device.Id, null, cancellationToken);
                    var existingAlert = existingAlerts.FirstOrDefault(a => a.ComponentKey == actuatorKey && a.ResolutionInfo == null);

                    IotAlert finalAlert;
                    bool shouldNotify = false;
                    int notificationCount = 1;
                    DateTime now = DateTime.UtcNow;

                    if (existingAlert != null)
                    {
                        finalAlert = existingAlert;
                        
                        // Extract throttling metadata from AlertInfo JSONB
                        var alertData = new Dictionary<string, object>();
                        if (finalAlert.AlertInfo != null)
                        {
                            try
                            {
                                alertData = JsonSerializer.Deserialize<Dictionary<string, object>>(finalAlert.AlertInfo.RootElement.GetRawText()) ?? new();
                            }
                            catch { }
                        }

                        notificationCount = alertData.TryGetValue("notificationCount", out var c) ? Convert.ToInt32(c.ToString()) : 1;
                        var lastSentStr = alertData.TryGetValue("lastNotificationAt", out var ls) ? ls.ToString() : null;
                        DateTime lastSent = string.IsNullOrEmpty(lastSentStr) ? finalAlert.CreatedAt ?? now : DateTime.Parse(lastSentStr);

                        var minutesSinceLast = (now - lastSent).TotalMinutes;

                        // Throttling logic: 0 - 2 - 30 scale
                        if (notificationCount == 1 && minutesSinceLast >= 2)
                        {
                            notificationCount = 2;
                            shouldNotify = true;
                        }
                        else if (notificationCount >= 2 && minutesSinceLast >= 30)
                        {
                            notificationCount++;
                            shouldNotify = true;
                        }

                        // Update Alert Content
                        var updatedInfo = new
                        {
                            severity = "CRITICAL",
                            title = "Rule Conflict Detected",
                            message = $"Emergency STOP sent to {actuatorKey}.",
                            description = $"Conflict detected between rules: {ruleNames}. System is preventing simultaneous control (Sensor: {request.ComponentKey}, Value: {request.Value}).",
                            solution = "Review your automation rules to resolve priority or condition overlaps.",
                            lastTriggeredAt = now.ToString("o"),
                            notificationCount = notificationCount,
                            lastNotificationAt = shouldNotify ? now.ToString("o") : lastSentStr,
                            conflictingRules = matchedRules.Select(r => new { id = r.Rule.Id, name = r.Rule.Name, actionType = r.ActionType }).ToList()
                        };
                        finalAlert.AlertInfo = JsonSerializer.SerializeToDocument(updatedInfo);
                        await _iotRepository.UpdateIotAlertAsync(finalAlert, cancellationToken);
                    }
                    else
                    {
                        // Create New Alert
                        shouldNotify = true;
                        finalAlert = new IotAlert
                        {
                            Id = Guid.NewGuid(),
                            DeviceId = device.Id,
                            ComponentKey = actuatorKey,
                            AlertInfo = JsonSerializer.SerializeToDocument(new
                            {
                                severity = "CRITICAL",
                                title = "Rule Conflict Detected",
                                message = $"Emergency STOP sent to {actuatorKey}.",
                                description = $"Conflict detected between rules: {ruleNames}. (Sensor: {request.ComponentKey}, Value: {request.Value}).",
                                solution = "Review your automation rules to resolve priority or condition overlaps.",
                                firstTriggeredAt = now.ToString("o"),
                                lastTriggeredAt = now.ToString("o"),
                                notificationCount = 1,
                                lastNotificationAt = now.ToString("o"),
                                triggeredRules = matchedRules.Select(r => new { id = r.Rule.Id, name = r.Rule.Name, actionType = r.ActionType }).ToList()
                            }),
                            CreatedAt = now
                        };
                        await _iotRepository.CreateIotAlertAsync(finalAlert, cancellationToken);
                    }

                    // 3. Notify Staff (triggers Email) only if throttling allows
                    if (shouldNotify)
                    {
                        await _publisher.Publish(new decorativeplant_be.Application.Features.IoT.Events.IotAlertTriggeredNotification
                        {
                            Device = device,
                            Alert = finalAlert,
                            RuleName = "SYSTEM CONFLICT DETECTOR"
                        }, cancellationToken);
                    }
                }
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}
