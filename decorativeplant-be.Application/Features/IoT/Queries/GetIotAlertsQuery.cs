using decorativeplant_be.Application.DTOs.IoT;
using MediatR;

namespace decorativeplant_be.Application.Features.IoT.Queries;

public class GetIotAlertsQuery : IRequest<IEnumerable<IotAlertDto>>
{
    public Guid? DeviceId { get; set; }
}
