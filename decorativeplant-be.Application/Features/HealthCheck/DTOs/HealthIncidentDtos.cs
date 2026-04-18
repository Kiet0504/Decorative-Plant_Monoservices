using System.Text.Json.Serialization;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Application.Features.HealthCheck.DTOs;

public class HealthIncidentDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("batchId")]
    public Guid? BatchId { get; set; }

    [JsonPropertyName("batchCode")]
    public string? BatchCode { get; set; }

    [JsonPropertyName("incidentType")]
    public string IncidentType { get; set; } = string.Empty;

    [JsonPropertyName("severity")]
    public string Severity { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("treatmentDetails")]
    public object? TreatmentDetails { get; set; }

    [JsonPropertyName("evidenceImages")]
    public object? EvidenceImages { get; set; }

    [JsonPropertyName("imageUrl")]
    public string? ImageUrl { get; set; }

    [JsonPropertyName("reportedAt")]
    public DateTime? ReportedAt { get; set; }

    [JsonPropertyName("reportedBy")]
    public Guid? ReportedBy { get; set; }

    [JsonPropertyName("reportedByName")]
    public string? ReportedByName { get; set; }

    [JsonPropertyName("resolvedAt")]
    public DateTime? ResolvedAt { get; set; }

    [JsonPropertyName("resolvedBy")]
    public Guid? ResolvedBy { get; set; }

    [JsonPropertyName("resolvedByName")]
    public string? ResolvedByName { get; set; }

    [JsonPropertyName("branchId")]
    public Guid? BranchId { get; set; }

    [JsonPropertyName("branchName")]
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
    public List<string>? ImageUrls { get; set; }
    public DateTime? ResolvedAt { get; set; }
}
