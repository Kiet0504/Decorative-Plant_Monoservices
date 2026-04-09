using decorativeplant_be.Domain.Entities;
using MediatR;

namespace decorativeplant_be.Application.Features.IoT.Events;

public class IotAlertTriggeredNotification : INotification
{
    public IotDevice Device { get; set; } = null!;
    public IotAlert Alert { get; set; } = null!;
    public string RuleName { get; set; } = string.Empty;
}
