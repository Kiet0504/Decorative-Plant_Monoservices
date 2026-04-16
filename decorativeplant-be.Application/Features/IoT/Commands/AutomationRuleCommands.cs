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

        var rules = await repo.GetAutomationRulesAsync(deviceId.Value, null, ct);
        var activeRules = rules.Where(r => r.IsActive).Select(ToDto).ToList();
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

        await AutomationRuleMqttNotifier.NotifyDeviceAsync(rule.DeviceId, _repo, _mqttService, cancellationToken);

        return true;
    }
}
