using decorativeplant_be.Application.DTOs.IoT;
using MediatR;

namespace decorativeplant_be.Application.Features.IoT.Queries;

public class GetAutomationRulesQuery : IRequest<IEnumerable<AutomationRuleDto>>
{
    public Guid? DeviceId { get; set; }
}

public class GetAutomationRuleByIdQuery : IRequest<AutomationRuleDto?>
{
    public Guid RuleId { get; set; }
}
