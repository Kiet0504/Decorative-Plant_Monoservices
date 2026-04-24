namespace decorativeplant_be.Application.Common.DTOs.Garden;

/// <summary>
/// A single item in the merged garden timeline (care logs, milestones, diagnoses).
/// </summary>
public class TimelineItemDto
{
    public Guid Id { get; set; }

    public DateTime? Date { get; set; }

    /// <summary>care|milestone|diagnosis</summary>
    public string Type { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? Summary { get; set; }

    /// <summary>Care logs only: how the plant seemed (from log_info.mood).</summary>
    public string? Mood { get; set; }

    public string? ImageUrl { get; set; }

    /// <summary>ID of the source entity (CareLog, milestone, or PlantDiagnosis).</summary>
    public Guid SourceId { get; set; }

    /// <summary>Diagnosis items only: when set, the issue was marked resolved (still shown in diary).</summary>
    public DateTime? ResolvedAtUtc { get; set; }

    /// <summary>Additional metadata.</summary>
    public object? Metadata { get; set; }
}
