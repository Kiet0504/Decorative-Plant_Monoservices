using decorativeplant_be.Application.Common.Interfaces;
using MediatR;

namespace decorativeplant_be.Application.Features.IoT.Commands;

public class ResolveIotAlertCommand : IRequest<bool>
{
    public Guid AlertId { get; set; }
    public System.Text.Json.JsonDocument? ResolutionInfo { get; set; }
}

public class ResolveIotAlertCommandHandler : IRequestHandler<ResolveIotAlertCommand, bool>
{
    private readonly IIotRepository _repo;
    private readonly IUnitOfWork _unitOfWork;

    public ResolveIotAlertCommandHandler(IIotRepository repo, IUnitOfWork unitOfWork)
    {
        _repo = repo;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(ResolveIotAlertCommand request, CancellationToken cancellationToken)
    {
        var alert = await _repo.GetIotAlertByIdAsync(request.AlertId, cancellationToken);
        if (alert == null) return false;

        alert.ResolutionInfo = request.ResolutionInfo;
        await _repo.UpdateIotAlertAsync(alert, cancellationToken);
        
        // --- CHOT GIAO DICH: Luu vao Database ---
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        
        return true;
    }
}
