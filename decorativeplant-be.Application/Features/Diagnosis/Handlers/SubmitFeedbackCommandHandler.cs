using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Diagnosis.Commands;
using MediatR;

namespace decorativeplant_be.Application.Features.Diagnosis.Handlers;

public class SubmitFeedbackCommandHandler : IRequestHandler<SubmitFeedbackCommand, Unit>
{
    private readonly IGardenRepository _gardenRepository;
    private readonly IUnitOfWork _unitOfWork;

    public SubmitFeedbackCommandHandler(
        IGardenRepository gardenRepository,
        IUnitOfWork unitOfWork)
    {
        _gardenRepository = gardenRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Unit> Handle(SubmitFeedbackCommand request, CancellationToken cancellationToken)
    {
        var diagnosis = await _gardenRepository.GetPlantDiagnosisByIdAsync(request.DiagnosisId, cancellationToken);
        if (diagnosis == null)
        {
            throw new NotFoundException("Diagnosis", request.DiagnosisId);
        }

        var isOwner = diagnosis.UserId == request.UserId ||
            (diagnosis.GardenPlant != null && diagnosis.GardenPlant.UserId == request.UserId);
        if (!isOwner)
        {
            throw new NotFoundException("Diagnosis", request.DiagnosisId);
        }

        diagnosis.Feedback = DiagnosisMapper.BuildFeedbackJson(request.UserFeedback, request.ExpertNotes);
        await _gardenRepository.UpdatePlantDiagnosisAsync(diagnosis, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
