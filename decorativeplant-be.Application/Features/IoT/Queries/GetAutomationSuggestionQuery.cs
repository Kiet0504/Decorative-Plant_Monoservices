using decorativeplant_be.Application.Features.IoT.DTOs;
using MediatR;

namespace decorativeplant_be.Application.Features.IoT.Queries;

public class GetAutomationSuggestionQuery : IRequest<List<AutomationSuggestionDto>>
{
    public Guid DeviceId { get; set; }
    public string? GrowthStage { get; set; }
}
