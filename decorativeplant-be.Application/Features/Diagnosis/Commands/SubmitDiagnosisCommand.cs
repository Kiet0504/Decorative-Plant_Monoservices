using decorativeplant_be.Application.Common.DTOs.Diagnosis;
using MediatR;

namespace decorativeplant_be.Application.Features.Diagnosis.Commands;

/// <summary>
/// Command to submit a plant image for AI diagnosis.
/// </summary>
public class SubmitDiagnosisCommand : IRequest<PlantDiagnosisDto>
{
    public Guid UserId { get; set; }

    /// <summary>URL of the plant image (client uploads to storage first).</summary>
    public string ImageUrl { get; set; } = string.Empty;

    public string? UserDescription { get; set; }

    /// <summary>Optional. Link diagnosis to a garden plant.</summary>
    public Guid? GardenPlantId { get; set; }
}
