using decorativeplant_be.Application.DTOs.IoT;
using decorativeplant_be.Application.Common.Interfaces;
using MediatR;

namespace decorativeplant_be.Application.Features.IoT.Queries;

public class GetDeviceRulesQuery : IRequest<IEnumerable<AutomationRuleDto>>
{
    public string DeviceSecret { get; set; } = string.Empty;
}

public class GetDeviceRulesQueryHandler : IRequestHandler<GetDeviceRulesQuery, IEnumerable<AutomationRuleDto>>
{
    private readonly IIotRepository _repo;

    public GetDeviceRulesQueryHandler(IIotRepository repo) => _repo = repo;

    public async Task<IEnumerable<AutomationRuleDto>> Handle(GetDeviceRulesQuery request, CancellationToken cancellationToken)
    {
        var device = await _repo.GetDeviceBySecretAsync(request.DeviceSecret, cancellationToken);
        if (device == null || device.Status != "Active")
        {
            throw new UnauthorizedAccessException("Invalid or inactive device.");
        }

        var rules = await _repo.GetAutomationRulesAsync(device.Id, cancellationToken);
        return rules.Where(r => r.IsActive).Select(ToDto);
    }

    private static AutomationRuleDto ToDto(decorativeplant_be.Domain.Entities.AutomationRule r) => new AutomationRuleDto
    {
        Id = r.Id, 
        DeviceId = r.DeviceId, 
        Name = r.Name,
        Priority = r.Priority, 
        IsActive = r.IsActive,
        Schedule = r.Schedule, 
        Conditions = r.Conditions, 
        Actions = r.Actions,
        CreatedAt = r.CreatedAt
    };
}
