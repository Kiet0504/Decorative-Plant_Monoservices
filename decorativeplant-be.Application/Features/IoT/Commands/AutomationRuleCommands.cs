using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.DTOs.IoT;
using decorativeplant_be.Domain.Entities;
using MediatR;
using System.Text.Json;

namespace decorativeplant_be.Application.Features.IoT.Commands;

public static class AutomationRuleMqttNotifier
{
    private static AutomationRuleDto ToDto(AutomationRule r) => new AutomationRuleDto
    {
        Id = r.Id, DeviceId = r.DeviceId, Name = r.Name, Priority = r.Priority, IsActive = r.IsActive, Schedule = r.Schedule, Conditions = r.Conditions, Actions = r.Actions, CreatedAt = r.CreatedAt
    };

    public static async Task NotifyDeviceAsync(Guid? deviceId, IIotRepository repo, IMqttService mqttService, CancellationToken ct)
    {
        if (deviceId == null) return;
        var device = await repo.GetIotDeviceByIdAsync(deviceId.Value, ct);
        if (device == null || string.IsNullOrEmpty(device.SecretKey)) return;

        // 1. Extract operating context from DeviceInfo
        string opSeason = "spring";
        string opStage = "seedling";
        bool isAutoEnabled = true;

        if (device.DeviceInfo != null)
        {
            try
            {
                var root = device.DeviceInfo.RootElement;
                if (root.TryGetProperty("operatingSeason", out var sProp) && sProp.ValueKind == JsonValueKind.String)
                    opSeason = sProp.GetString() ?? "spring";
                
                if (root.TryGetProperty("operatingStage", out var stProp) && stProp.ValueKind == JsonValueKind.String)
                    opStage = stProp.GetString() ?? "seedling";

                if (root.TryGetProperty("isAutomationEnabled", out var aProp))
                {
                    if (aProp.ValueKind == JsonValueKind.True) isAutoEnabled = true;
                    else if (aProp.ValueKind == JsonValueKind.False) isAutoEnabled = false;
                }
            }
            catch { }
        }

        // 2. Fetch and filter rules
        var rules = await repo.GetAutomationRulesAsync(deviceId.Value, null, ct);
        
        List<AutomationRuleDto> activeRules;
        if (!isAutoEnabled)
        {
            activeRules = new List<AutomationRuleDto>(); // Master Toggle OFF -> No rules run
        }
        else
        {
            activeRules = rules
                .Where(r => r.IsActive)
                .Where(r => {
                    var name = r.Name?.ToUpper() ?? "";
                    bool hasTags = name.Contains("[") && name.Contains("]");
                    if (!hasTags) return true; // Global rules without tags always run if active

                    bool seasonMatch = name.Contains($"[{opSeason.ToUpper()}]");
                    bool stageMatch = name.Contains($"[{opStage.ToUpper()}]");
                    return seasonMatch && stageMatch;
                })
                .Select(ToDto)
                .ToList();
        }

        var json = JsonSerializer.Serialize(activeRules, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        await mqttService.PublishRulesUpdateAsync(device.SecretKey, json, ct);
    }
}

// --- Create ---
public class CreateAutomationRuleCommand : IRequest<AutomationRuleDto>
{
    public CreateAutomationRuleDto Dto { get; set; } = null!;
}

public class CreateAutomationRuleCommandHandler : IRequestHandler<CreateAutomationRuleCommand, AutomationRuleDto>
{
    private readonly IIotRepository _repo;
    private readonly IMqttService _mqttService;
    private readonly IUnitOfWork _unitOfWork;

    public CreateAutomationRuleCommandHandler(IIotRepository repo, IMqttService mqttService, IUnitOfWork unitOfWork) 
    {
        _repo = repo;
        _mqttService = mqttService;
        _unitOfWork = unitOfWork;
    }

    public async Task<AutomationRuleDto> Handle(CreateAutomationRuleCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Dto;
        var rule = new AutomationRule
        {
            Id = Guid.NewGuid(),
            DeviceId = dto.DeviceId,
            Name = dto.Name,
            Priority = dto.Priority,
            IsActive = dto.IsActive,
            Schedule = dto.Schedule,
            Conditions = dto.Conditions,
            Actions = dto.Actions,
            CreatedAt = DateTime.UtcNow
        };
        var created = await _repo.CreateAutomationRuleAsync(rule, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await AutomationRuleMqttNotifier.NotifyDeviceAsync(created.DeviceId, _repo, _mqttService, cancellationToken);

        return new AutomationRuleDto { Id = created.Id, DeviceId = created.DeviceId, Name = created.Name, Priority = created.Priority, IsActive = created.IsActive, Schedule = created.Schedule, Conditions = created.Conditions, Actions = created.Actions, CreatedAt = created.CreatedAt };
    }
}

// --- Update ---
public class UpdateAutomationRuleCommand : IRequest<bool>
{
    public Guid RuleId { get; set; }
    public UpdateAutomationRuleDto Dto { get; set; } = null!;
}

public class UpdateAutomationRuleCommandHandler : IRequestHandler<UpdateAutomationRuleCommand, bool>
{
    private readonly IIotRepository _repo;
    private readonly IMqttService _mqttService;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateAutomationRuleCommandHandler(IIotRepository repo, IMqttService mqttService, IUnitOfWork unitOfWork) 
    {
        _repo = repo;
        _mqttService = mqttService;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(UpdateAutomationRuleCommand request, CancellationToken cancellationToken)
    {
        var rule = await _repo.GetAutomationRuleByIdAsync(request.RuleId, cancellationToken);
        if (rule == null) return false;

        bool wasActive = rule.IsActive;
        var dto = request.Dto;
        rule.DeviceId = dto.DeviceId;
        rule.Name = dto.Name;
        rule.Priority = dto.Priority;
        rule.IsActive = dto.IsActive;
        rule.Schedule = dto.Schedule;
        rule.Conditions = dto.Conditions;
        rule.Actions = dto.Actions;

        await _repo.UpdateAutomationRuleAsync(rule, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // If rule was disabled, send a STOP command to affected actuators to ensure they stop immediately
        if (wasActive && !rule.IsActive && rule.Actions != null && rule.DeviceId.HasValue)
        {
            try
            {
                var device = await _repo.GetIotDeviceByIdAsync(rule.DeviceId.Value, cancellationToken);
                if (device != null)
                {
                    var actionsArray = rule.Actions.RootElement.ValueKind == JsonValueKind.Array 
                        ? rule.Actions.RootElement.EnumerateArray() 
                        : (rule.Actions.RootElement.TryGetProperty("actions", out var ap) ? ap.EnumerateArray() : Enumerable.Empty<JsonElement>());

                    foreach (var action in actionsArray)
                    {
                        var actuatorKey = action.TryGetProperty("target_component_key", out var ak) ? ak.GetString() : null;
                        if (!string.IsNullOrEmpty(actuatorKey))
                        {
                            await _mqttService.PublishCommandAsync(device.SecretKey, actuatorKey, new { value = "turn_off", note = "Rule disabled by user" }, cancellationToken);
                        }
                    }
                }
            }
            catch (Exception ex) { Console.WriteLine($"[Sync Error] Failed to send stop command: {ex.Message}"); }
        }

        await AutomationRuleMqttNotifier.NotifyDeviceAsync(rule.DeviceId, _repo, _mqttService, cancellationToken);

        return true;
    }
}

// --- Delete ---
public class DeleteAutomationRuleCommand : IRequest<bool>
{
    public Guid RuleId { get; set; }
}

public class DeleteAutomationRuleCommandHandler : IRequestHandler<DeleteAutomationRuleCommand, bool>
{
    private readonly IIotRepository _repo;
    private readonly IMqttService _mqttService;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteAutomationRuleCommandHandler(IIotRepository repo, IMqttService mqttService, IUnitOfWork unitOfWork) 
    {
        _repo = repo;
        _mqttService = mqttService;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(DeleteAutomationRuleCommand request, CancellationToken cancellationToken)
    {
        var rule = await _repo.GetAutomationRuleByIdAsync(request.RuleId, cancellationToken);
        if (rule == null) return false;
        
        await _repo.DeleteAutomationRuleAsync(rule, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Send a STOP command to affected actuators if the rule was active
        if (rule.IsActive && rule.Actions != null && rule.DeviceId.HasValue)
        {
            try
            {
                var device = await _repo.GetIotDeviceByIdAsync(rule.DeviceId.Value, cancellationToken);
                if (device != null)
                {
                    var actionsArray = rule.Actions.RootElement.ValueKind == JsonValueKind.Array 
                        ? rule.Actions.RootElement.EnumerateArray() 
                        : (rule.Actions.RootElement.TryGetProperty("actions", out var ap) ? ap.EnumerateArray() : Enumerable.Empty<JsonElement>());

                    foreach (var action in actionsArray)
                    {
                        var actuatorKey = action.TryGetProperty("target_component_key", out var ak) ? ak.GetString() : null;
                        if (!string.IsNullOrEmpty(actuatorKey))
                        {
                            await _mqttService.PublishCommandAsync(device.SecretKey, actuatorKey, new { value = "turn_off", note = "Rule deleted by user" }, cancellationToken);
                        }
                    }
                }
            }
            catch (Exception ex) { Console.WriteLine($"[Sync Error] Failed to send stop command on delete: {ex.Message}"); }
        }

        await AutomationRuleMqttNotifier.NotifyDeviceAsync(rule.DeviceId, _repo, _mqttService, cancellationToken);

        return true;
    }
}
