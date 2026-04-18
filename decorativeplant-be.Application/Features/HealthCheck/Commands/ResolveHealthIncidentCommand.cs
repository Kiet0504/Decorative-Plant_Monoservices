using decorativeplant_be.Application.Features.HealthCheck.DTOs;
using MediatR;

namespace decorativeplant_be.Application.Features.HealthCheck.Commands;

public class ResolveHealthIncidentCommand : IRequest<HealthIncidentDto>
{
    public Guid Id { get; set; }
    public string? Status { get; set; }
    public string ResolutionNotes { get; set; } = string.Empty;
    public Dictionary<string, object>? TreatmentDetails { get; set; }
    public List<string>? ImageUrls { get; set; }
    public DateTime? ResolvedAt { get; set; }
    
    // Internal
    public Guid? ResolvedBy { get; set; }
    public bool IsManagerApproval { get; set; } = false;
}
