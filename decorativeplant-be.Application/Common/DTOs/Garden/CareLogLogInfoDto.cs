using System.Text.Json.Serialization;

namespace decorativeplant_be.Application.Common.DTOs.Garden;

/// <summary>
/// Log info for a care log. Maps to care_log.log_info JSONB.
/// </summary>
public class CareLogLogInfoDto
{
    /// <summary>Preset slug or free-text care label.</summary>
    [JsonPropertyName("action_type")]
    public string? ActionType { get; set; }

    public string? Description { get; set; }

    /// <summary>Flexible object e.g. { "fertilizer": "NPK 10-10-10", "amount": "1 tbsp" }</summary>
    public object? Products { get; set; }

    public string? Observations { get; set; }

    /// <summary>Preset slug or free-text mood.</summary>
    public string? Mood { get; set; }
}
