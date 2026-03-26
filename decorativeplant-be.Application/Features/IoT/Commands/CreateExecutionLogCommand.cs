using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Domain.Entities;
using MediatR;
using System.Text.Json;

namespace decorativeplant_be.Application.Features.IoT.Commands;

public class CreateExecutionLogCommand : IRequest<bool>
{
    public string DeviceSecret { get; set; } = string.Empty;
    public Guid RuleId { get; set; }
    public JsonDocument? ExecutionInfo { get; set; }
}

public class CreateExecutionLogCommandHandler : IRequestHandler<CreateExecutionLogCommand, bool>
{
    private readonly IIotRepository _repo;
    private readonly IUnitOfWork _unitOfWork;

    public CreateExecutionLogCommandHandler(IIotRepository repo, IUnitOfWork unitOfWork) 
    {
        _repo = repo;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(CreateExecutionLogCommand request, CancellationToken cancellationToken)
    {
        var device = await _repo.GetDeviceBySecretAsync(request.DeviceSecret, cancellationToken);
        if (device == null || device.Status != "Active")
        {
            throw new UnauthorizedAccessException("Invalid or inactive device.");
        }

        var log = new AutomationExecutionLog
        {
            Id = Guid.NewGuid(),
            RuleId = request.RuleId,
            ExecutionInfo = request.ExecutionInfo,
            ExecutedAt = DateTime.UtcNow
        };

        await _repo.AddExecutionLogAsync(log, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}
