using decorativeplant_be.Application.Common.DTOs.Diagnosis;
using decorativeplant_be.Application.Common.DTOs.Garden;
using MediatR;

namespace decorativeplant_be.Application.Features.Diagnosis.Queries;

/// <summary>
/// Query to list diagnoses for the current user (with optional plant filter).
/// </summary>
public class GetDiagnosesQuery : IRequest<PagedResultDto<PlantDiagnosisDto>>
{
    public Guid UserId { get; set; }

    /// <summary>Optional. Filter by garden plant.</summary>
    public Guid? GardenPlantId { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}
