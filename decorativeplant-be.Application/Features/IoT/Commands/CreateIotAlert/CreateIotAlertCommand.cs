using System.Text.Json;
using decorativeplant_be.Application.DTOs.IoT;
using MediatR;

namespace decorativeplant_be.Application.Features.IoT.Commands.CreateIotAlert;

public class CreateIotAlertCommand : IRequest<IotAlertDto>
{
    public Guid? DeviceId { get; set; }
    public string? ComponentKey { get; set; }
    public JsonDocument? AlertInfo { get; set; }
}
