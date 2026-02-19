using System.Text.Json.Serialization;

namespace decorativeplant_be.Application.Common.DTOs.Diagnosis;

/// <summary>
/// Response DTO for a plant diagnosis.
/// </summary>
public class PlantDiagnosisDto
{
    public Guid Id { get; set; }

    public Guid? UserId { get; set; }

    public Guid? GardenPlantId { get; set; }

    public PlantDiagnosisUserInputDto? UserInput { get; set; }

    public PlantDiagnosisAiResultDto? AiResult { get; set; }

    public PlantDiagnosisFeedbackDto? Feedback { get; set; }

    public DateTime? CreatedAt { get; set; }
}

/// <summary>
/// User input for diagnosis. Maps to plant_diagnosis.user_input JSONB.
/// </summary>
public class PlantDiagnosisUserInputDto
{
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("image_urls")]
    public List<string> ImageUrls { get; set; } = new();
}

/// <summary>
/// AI analysis result. Maps to plant_diagnosis.ai_result JSONB.
/// </summary>
public class PlantDiagnosisAiResultDto
{
    public string Disease { get; set; } = string.Empty;

    public double Confidence { get; set; }

    public List<string> Symptoms { get; set; } = new();

    public List<string> Recommendations { get; set; } = new();

    public string? Explanation { get; set; }
}

/// <summary>
/// User/expert feedback. Maps to plant_diagnosis.feedback JSONB.
/// </summary>
public class PlantDiagnosisFeedbackDto
{
    /// <summary>helpful|not_helpful|wrong</summary>
    [JsonPropertyName("user_feedback")]
    public string? UserFeedback { get; set; }

    [JsonPropertyName("expert_notes")]
    public string? ExpertNotes { get; set; }
}
