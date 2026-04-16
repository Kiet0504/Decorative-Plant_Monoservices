using decorativeplant_be.Application.DTOs.IoT;
using MediatR;

namespace decorativeplant_be.Application.Features.IoT.Queries.GetIotDevices;

public class GetIotDevicesQuery : IRequest<IEnumerable<IotDeviceDto>>
{
    public Guid? BranchId { get; set; }
}
