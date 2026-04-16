using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Application.Features.HealthCheck.DTOs;

public class HealthIncidentDto
{
    public Guid Id { get; set; }
    public Guid? BatchId { get; set; }
    public string? BatchCode { get; set; }
    public string IncidentType { get; set; } = string.Empty; // Pest, Disease, Nutrient, Physical
    public string Severity { get; set; } = string.Empty; // Low, Medium, High, Critical
    public string Status { get; set; } = string.Empty; // Reported, InTreatment, Resolved
    public string? Description { get; set; }
    public object? TreatmentDetails { get; set; } // JSONB
    public object? EvidenceImages { get; set; } // JSONB
    public string? ImageUrl { get; set; }
    public DateTime? ReportedAt { get; set; }
    public Guid? ReportedBy { get; set; }
    public string? ReportedByName { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public Guid? ResolvedBy { get; set; }
    public string? ResolvedByName { get; set; }
    public Guid? BranchId { get; set; }
    public string? BranchName { get; set; }
}

public class CreateHealthIncidentDto
{
    public Guid BatchId { get; set; }
    public string IncidentType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<string>? ImageUrls { get; set; }
    public DateTime? ReportedAt { get; set; }
}

public class ResolveHealthIncidentDto
{
    public Guid Id { get; set; }
    public string? Status { get; set; } // New field to support InTreatment, Monitoring, etc.
    public string ResolutionNotes { get; set; } = string.Empty;
    public Dictionary<string, object>? TreatmentDetails { get; set; }
    public DateTime? ResolvedAt { get; set; }
}
