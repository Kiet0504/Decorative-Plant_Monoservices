using System.Text.Json;
using decorativeplant_be.Application.Common.DTOs.Diagnosis;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Application.Features.Diagnosis;

/// <summary>
/// Maps PlantDiagnosis entity to DTOs. Handles JSONB serialization.
/// </summary>
public static class DiagnosisMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static PlantDiagnosisDto ToDto(PlantDiagnosis d)
    {
        PlantDiagnosisUserInputDto? userInput = null;
        if (d.UserInput != null)
        {
            userInput = JsonSerializer.Deserialize<PlantDiagnosisUserInputDto>(d.UserInput.RootElement.GetRawText(), JsonOptions);
        }

        PlantDiagnosisAiResultDto? aiResult = null;
        if (d.AiResult != null)
        {
            aiResult = JsonSerializer.Deserialize<PlantDiagnosisAiResultDto>(d.AiResult.RootElement.GetRawText(), JsonOptions);
        }

        PlantDiagnosisFeedbackDto? feedback = null;
        if (d.Feedback != null)
        {
            feedback = JsonSerializer.Deserialize<PlantDiagnosisFeedbackDto>(d.Feedback.RootElement.GetRawText(), JsonOptions);
        }

        return new PlantDiagnosisDto
        {
            Id = d.Id,
            UserId = d.UserId,
            GardenPlantId = d.GardenPlantId,
            UserInput = userInput,
            AiResult = aiResult,
            Feedback = feedback,
            CreatedAt = d.CreatedAt,
            ResolvedAtUtc = d.ResolvedAtUtc
        };
    }

    /// <summary>
    /// Builds UserInput JsonDocument from image URL and description.
    /// </summary>
    public static JsonDocument? BuildUserInputJson(string imageUrl, string? description)
    {
        var urls = string.IsNullOrWhiteSpace(imageUrl) ? Array.Empty<string>() : new[] { imageUrl };
        var dict = new Dictionary<string, object?> { ["image_urls"] = urls };
        if (!string.IsNullOrEmpty(description)) dict["description"] = description;

        var json = JsonSerializer.SerializeToUtf8Bytes(dict, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
        return JsonDocument.Parse(json);
    }

    /// <summary>
    /// Builds AiResult JsonDocument from IAiDiagnosisService result.
    /// </summary>
    /// <summary>Persist AI Hub / chat diagnosis without re-running vision models.</summary>
    public static JsonDocument BuildAiResultJsonFromSummaryDto(PlantDiagnosisAiResultDto dto)
    {
        var dict = new Dictionary<string, object>
        {
            ["disease"] = dto.Disease,
            ["confidence"] = dto.Confidence,
            ["symptoms"] = dto.Symptoms ?? new List<string>(),
            ["recommendations"] = dto.Recommendations ?? new List<string>()
        };
        if (!string.IsNullOrEmpty(dto.Explanation)) dict["explanation"] = dto.Explanation;

        var json = JsonSerializer.SerializeToUtf8Bytes(dict, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
        return JsonDocument.Parse(json);
    }

    public static JsonDocument BuildAiResultJson(AiDiagnosisResultDto result)
    {
        var dict = new Dictionary<string, object>
        {
            ["disease"] = result.Disease,
            ["confidence"] = result.Confidence,
            ["symptoms"] = result.Symptoms,
            ["recommendations"] = result.Recommendations
        };
        if (!string.IsNullOrEmpty(result.Explanation)) dict["explanation"] = result.Explanation;

        var json = JsonSerializer.SerializeToUtf8Bytes(dict, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
        return JsonDocument.Parse(json);
    }

    /// <summary>
    /// Builds Feedback JsonDocument.
    /// </summary>
    public static JsonDocument BuildFeedbackJson(string userFeedback, string? expertNotes)
    {
        var dict = new Dictionary<string, object?>
        {
            ["user_feedback"] = userFeedback
        };
        if (!string.IsNullOrEmpty(expertNotes)) dict["expert_notes"] = expertNotes;

        var json = JsonSerializer.SerializeToUtf8Bytes(dict, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
        return JsonDocument.Parse(json);
    }
}
