using decorativeplant_be.Application.Common.DTOs.Garden;
using MediatR;

namespace decorativeplant_be.Application.Features.Diagnosis.Queries;

/// <summary>
/// Generates an AI recovery-aware schedule plan from a previously saved <see cref="Domain.Entities.PlantDiagnosis"/>.
/// This avoids re-running vision/diagnosis models and only calls the scheduler on demand.
/// </summary>
public sealed class GenerateRecoveryScheduleFromDiagnosisQuery : IRequest<AiSchedulePlanDto>
{
    public Guid UserId { get; set; }
    public Guid DiagnosisId { get; set; }
    public int HorizonDays { get; set; } = 30;
    public int? UtcOffsetMinutes { get; set; }
}

