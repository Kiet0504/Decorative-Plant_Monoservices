namespace decorativeplant_be.Application.Features.Cultivation.DTOs;

public class CultivationLogDto
{
    public Guid Id { get; set; }
    public Guid? BatchId { get; set; }
    public string? BatchCode { get; set; }
    public Guid? LocationId { get; set; }
    public string? LocationName { get; set; }
    public string ActivityType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public object? Details { get; set; } // JSONB
    public Guid? PerformedBy { get; set; }
    public string? PerformedByName { get; set; }
    public DateTime? PerformedAt { get; set; }
}

public class CreateCultivationLogDto
{
    public Guid? BatchId { get; set; }
    public Guid? LocationId { get; set; }
    public string ActivityType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Dictionary<string, object>? Details { get; set; }
    public DateTime? PerformedAt { get; set; }
}

// DTOs for Daily Care (reusing CultivationLog)
public class BatchCareTaskDto
{
    public Guid Id { get; set; }
    public string ProductName { get; set; } = string.Empty; // From Batch.Taxonomy or Details
    public string Activity { get; set; } = string.Empty;    // ActivityType
    public string Batch { get; set; } = string.Empty;       // BatchCode
    public string Frequency { get; set; } = string.Empty;   // From Details
    public string Date { get; set; } = string.Empty;        // DueDate from Details
    public string Status { get; set; } = "Pending";         // Based on PerformedAt + Details.Status
    public string RepeatEvery { get; set; } = string.Empty; // From Details
}

public class BatchCareTaskDetailDto : BatchCareTaskDto
{
    public string Description { get; set; } = string.Empty;
    public string CareRequirement { get; set; } = string.Empty; // From Details
}

public class BatchCareTasksSummary
{
    public int TodayTask { get; set; }
    public int Watering { get; set; }
    public int Fertilizing { get; set; }
    public int PruningRepotting { get; set; }
}
