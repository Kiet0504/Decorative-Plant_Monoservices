using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Diagnosis.Commands;
using MediatR;

namespace decorativeplant_be.Application.Features.Diagnosis.Handlers;

public sealed class ResolvePlantDiagnosisCommandHandler : IRequestHandler<ResolvePlantDiagnosisCommand, Unit>
{
    private readonly IGardenRepository _gardenRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ResolvePlantDiagnosisCommandHandler(IGardenRepository gardenRepository, IUnitOfWork unitOfWork)
    {
        _gardenRepository = gardenRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Unit> Handle(ResolvePlantDiagnosisCommand request, CancellationToken cancellationToken)
    {
        var d = await _gardenRepository.GetPlantDiagnosisByIdAsync(request.DiagnosisId, cancellationToken);
        if (d == null || d.UserId != request.UserId)
        {
            throw new NotFoundException("Plant diagnosis", request.DiagnosisId);
        }

        d.ResolvedAtUtc = DateTime.UtcNow;
        await _gardenRepository.UpdatePlantDiagnosisAsync(d, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
