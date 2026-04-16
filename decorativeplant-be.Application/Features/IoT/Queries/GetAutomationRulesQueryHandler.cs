using decorativeplant_be.Application.DTOs.IoT;
using decorativeplant_be.Application.Common.Interfaces;
using MediatR;

namespace decorativeplant_be.Application.Features.IoT.Queries;

public class GetAutomationRulesQueryHandler : IRequestHandler<GetAutomationRulesQuery, IEnumerable<AutomationRuleDto>>
{
    private readonly IIotRepository _repo;
    public GetAutomationRulesQueryHandler(IIotRepository repo) => _repo = repo;

    public async Task<IEnumerable<AutomationRuleDto>> Handle(GetAutomationRulesQuery request, CancellationToken cancellationToken)
    {
        var rules = await _repo.GetAutomationRulesAsync(request.DeviceId, request.BranchId, cancellationToken);
        return rules.Select(ToDto);
    }

    private static AutomationRuleDto ToDto(decorativeplant_be.Domain.Entities.AutomationRule r) => new()
    {
        Id = r.Id, DeviceId = r.DeviceId, Name = r.Name,
        Priority = r.Priority, IsActive = r.IsActive,
        Schedule = r.Schedule, Conditions = r.Conditions, Actions = r.Actions,
        BranchId = r.Device?.BranchId,
        BranchName = r.Device?.Branch?.Name,
        CreatedAt = r.CreatedAt
    };
}

public class GetAutomationRuleByIdQueryHandler : IRequestHandler<GetAutomationRuleByIdQuery, AutomationRuleDto?>
{
    private readonly IIotRepository _repo;
    public GetAutomationRuleByIdQueryHandler(IIotRepository repo) => _repo = repo;

    public async Task<AutomationRuleDto?> Handle(GetAutomationRuleByIdQuery request, CancellationToken cancellationToken)
    {
        var r = await _repo.GetAutomationRuleByIdAsync(request.RuleId, cancellationToken);
        if (r == null) return null;
        return new AutomationRuleDto { Id = r.Id, DeviceId = r.DeviceId, Name = r.Name, Priority = r.Priority, IsActive = r.IsActive, Schedule = r.Schedule, Conditions = r.Conditions, Actions = r.Actions, CreatedAt = r.CreatedAt };
    }
}
