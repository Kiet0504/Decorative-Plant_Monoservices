using decorativeplant_be.Application.Features.HealthCheck.DTOs;
using MediatR;

namespace decorativeplant_be.Application.Features.HealthCheck.Commands;

public class ReportHealthIncidentCommand : IRequest<HealthIncidentDto>
{
    public Guid BatchId { get; set; }
    public string IncidentType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<string>? ImageUrls { get; set; }
    public DateTime? ReportedAt { get; set; }
    
    // Internal
    public Guid? ReportedBy { get; set; }
}
