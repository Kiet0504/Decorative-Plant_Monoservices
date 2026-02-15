using decorativeplant_be.Application.Common.DTOs.Diagnosis;
using MediatR;

namespace decorativeplant_be.Application.Features.Diagnosis.Queries;

/// <summary>
/// Query to get a single diagnosis by ID.
/// </summary>
public class GetDiagnosisQuery : IRequest<PlantDiagnosisDto?>
{
    public Guid UserId { get; set; }

    public Guid Id { get; set; }
}
