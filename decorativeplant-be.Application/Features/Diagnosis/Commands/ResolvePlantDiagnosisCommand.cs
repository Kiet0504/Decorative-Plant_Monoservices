using MediatR;

namespace decorativeplant_be.Application.Features.Diagnosis.Commands;

public sealed class ResolvePlantDiagnosisCommand : IRequest<Unit>
{
    public Guid UserId { get; set; }
    public Guid DiagnosisId { get; set; }
}
