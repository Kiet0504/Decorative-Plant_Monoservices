using decorativeplant_be.Application.DTOs.IoT;
using decorativeplant_be.Application.Common.Interfaces;
using MediatR;

namespace decorativeplant_be.Application.Features.IoT.Queries;

public class GetIotAlertsQueryHandler : IRequestHandler<GetIotAlertsQuery, IEnumerable<IotAlertDto>>
{
    private readonly IIotRepository _repo;
    public GetIotAlertsQueryHandler(IIotRepository repo) => _repo = repo;

    public async Task<IEnumerable<IotAlertDto>> Handle(GetIotAlertsQuery request, CancellationToken cancellationToken)
    {
        var alerts = await _repo.GetIotAlertsAsync(request.DeviceId, request.BranchId, cancellationToken);
        return alerts.Select(a => new IotAlertDto
        {
            Id = a.Id,
            DeviceId = a.DeviceId,
            ComponentKey = a.ComponentKey,
            AlertInfo = a.AlertInfo,
            ResolutionInfo = a.ResolutionInfo,
            BranchId = a.Device?.BranchId,
            BranchName = a.Device?.Branch?.Name,
            CreatedAt = a.CreatedAt
        });
    }
}
