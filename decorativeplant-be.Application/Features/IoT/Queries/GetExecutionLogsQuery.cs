using decorativeplant_be.Application.DTOs.IoT;
using decorativeplant_be.Application.Common.Interfaces;
using MediatR;

namespace decorativeplant_be.Application.Features.IoT.Queries;

public class GetExecutionLogsQuery : IRequest<IEnumerable<AutomationExecutionLogDto>>
{
    public Guid? RuleId { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
}

public class GetExecutionLogsQueryHandler : IRequestHandler<GetExecutionLogsQuery, IEnumerable<AutomationExecutionLogDto>>
{
    private readonly IIotRepository _repo;
    public GetExecutionLogsQueryHandler(IIotRepository repo) => _repo = repo;

    public async Task<IEnumerable<AutomationExecutionLogDto>> Handle(GetExecutionLogsQuery request, CancellationToken cancellationToken)
    {
        var logs = await _repo.GetExecutionLogsAsync(request.RuleId, request.From, request.To, cancellationToken);
        return logs.Select(l => new AutomationExecutionLogDto
        {
            Id = l.Id,
            RuleId = l.RuleId,
            ExecutionInfo = l.ExecutionInfo,
            ExecutedAt = l.ExecutedAt
        });
    }
}
