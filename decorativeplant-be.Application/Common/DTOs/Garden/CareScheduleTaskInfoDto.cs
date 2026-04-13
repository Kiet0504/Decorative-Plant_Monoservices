using System.Text.Json.Serialization;

namespace decorativeplant_be.Application.Common.DTOs.Garden;

/// <summary>
/// Typed shape for care_schedule.task_info JSONB.
/// See docs/JSONB_SCHEMA_REFERENCE.md#58-care_scheduletask_info
/// </summary>
public sealed class CareScheduleTaskInfoDto
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "water";

    [JsonPropertyName("frequency")]
    public string Frequency { get; set; } = "weekly";

    /// <summary>
    /// Preferred repeat interval in days (optional).
    /// When present, this is the authoritative cadence for scheduling and auto-advance.
    /// </summary>
    [JsonPropertyName("interval_days")]
    public int? IntervalDays { get; set; }

    [JsonPropertyName("time_of_day")]
    public string? TimeOfDay { get; set; }

    /// <summary>
    /// Optional hint for the backend to place first due date.
    /// Interpreted as days from the planning window start.
    /// </summary>
    [JsonPropertyName("suggested_offset_days")]
    public int? SuggestedOffsetDays { get; set; }

    /// <summary>
    /// Preferred name for offset days in planner output (optional).
    /// Interpreted as days from the planning window start.
    /// </summary>
    [JsonPropertyName("offset_days")]
    public int? OffsetDays { get; set; }

    /// <summary>ISO UTC date time string.</summary>
    [JsonPropertyName("next_due")]
    public DateTime? NextDue { get; set; }
}

