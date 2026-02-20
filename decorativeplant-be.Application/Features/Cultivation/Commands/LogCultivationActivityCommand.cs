using decorativeplant_be.Application.Features.Cultivation.DTOs;
using MediatR;

namespace decorativeplant_be.Application.Features.Cultivation.Commands;

public class LogCultivationActivityCommand : IRequest<CultivationLogDto>
{
    public Guid? BatchId { get; set; }
    public Guid? LocationId { get; set; }
    public string ActivityType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Dictionary<string, object>? Details { get; set; }
    public DateTime? PerformedAt { get; set; }
    
    // Internal use (set by controller/handler)
    public Guid? PerformedBy { get; set; }
}
