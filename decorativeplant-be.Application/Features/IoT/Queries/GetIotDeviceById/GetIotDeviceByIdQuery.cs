using decorativeplant_be.Application.DTOs.IoT;
using MediatR;

namespace decorativeplant_be.Application.Features.IoT.Queries.GetIotDeviceById;

public class GetIotDeviceByIdQuery : IRequest<IotDeviceDto?>
{
    public Guid Id { get; set; }

    public GetIotDeviceByIdQuery(Guid id)
    {
        Id = id;
    }
}
