using decorativeplant_be.Application.Features.HealthCheck.DTOs;
using MediatR;

namespace decorativeplant_be.Application.Features.HealthCheck.Commands;

public class UpdateHealthIncidentTreatmentCommand : IRequest<HealthIncidentDto>
{
    public Guid Id { get; set; }
    public string NewStatus { get; set; } = "InTreatment"; // Reported, InTreatment, Resolved
    public decimal? TreatmentCost { get; set; }
    public string? TreatmentNotes { get; set; }
    public Dictionary<string, object>? AdditionalTreatmentDetails { get; set; }
    
    // Internal user info
    public Guid? PerformedBy { get; set; }
}
