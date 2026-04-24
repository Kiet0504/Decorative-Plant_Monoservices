using decorativeplant_be.Application.Common.DTOs.Diagnosis;
using MediatR;

namespace decorativeplant_be.Application.Features.Diagnosis.Commands;

/// <summary>Save an AI Hub / chat diagnosis to the plant record (no new vision inference).</summary>
public sealed class SaveChatDiagnosisCommand : IRequest<PlantDiagnosisDto>
{
    public Guid UserId { get; set; }
    public Guid GardenPlantId { get; set; }
    /// <summary>Optional photo URL from chat attachment (S3 or absolute).</summary>
    public string? ImageUrl { get; set; }
    public PlantDiagnosisAiResultDto AiResult { get; set; } = new();
}
