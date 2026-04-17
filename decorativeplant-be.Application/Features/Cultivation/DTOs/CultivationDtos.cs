using System.Text.Json.Serialization;

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
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("productName")]
    public string ProductName { get; set; } = string.Empty;

    [JsonPropertyName("activity")]
    public string Activity { get; set; } = string.Empty;

    [JsonPropertyName("batch")]
    public string Batch { get; set; } = string.Empty;

    [JsonPropertyName("batchId")]
    public Guid? BatchId { get; set; }

    [JsonPropertyName("frequency")]
    public string Frequency { get; set; } = string.Empty;

    [JsonPropertyName("date")]
    public string Date { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = "Pending";

    [JsonPropertyName("repeatEvery")]
    public string RepeatEvery { get; set; } = string.Empty;

    [JsonPropertyName("branchId")]
    public Guid? BranchId { get; set; }

    [JsonPropertyName("branchName")]
    public string? BranchName { get; set; }
}

public class BatchCareTaskDetailDto : BatchCareTaskDto
{
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("careRequirement")]
    public string CareRequirement { get; set; } = string.Empty;
}

public class BatchCareTasksSummary
{
    [JsonPropertyName("todayTask")]
    public int TodayTask { get; set; }

    [JsonPropertyName("watering")]
    public int Watering { get; set; }

    [JsonPropertyName("fertilizing")]
    public int Fertilizing { get; set; }

    [JsonPropertyName("pruningRepotting")]
    public int PruningRepotting { get; set; }
}
