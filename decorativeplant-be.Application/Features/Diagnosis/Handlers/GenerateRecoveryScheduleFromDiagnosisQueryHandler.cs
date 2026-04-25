using decorativeplant_be.Application.Common.DTOs.Garden;
using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Diagnosis.Queries;
using decorativeplant_be.Application.Features.Garden.Queries;
using decorativeplant_be.Application.Features.Diagnosis;
using MediatR;

namespace decorativeplant_be.Application.Features.Diagnosis.Handlers;

public sealed class GenerateRecoveryScheduleFromDiagnosisQueryHandler
    : IRequestHandler<GenerateRecoveryScheduleFromDiagnosisQuery, AiSchedulePlanDto>
{
    private readonly IGardenRepository _gardenRepository;
    private readonly IMediator _mediator;

    public GenerateRecoveryScheduleFromDiagnosisQueryHandler(IGardenRepository gardenRepository, IMediator mediator)
    {
        _gardenRepository = gardenRepository;
        _mediator = mediator;
    }

    public async Task<AiSchedulePlanDto> Handle(
        GenerateRecoveryScheduleFromDiagnosisQuery request,
        CancellationToken cancellationToken)
    {
        var diagnosis = await _gardenRepository.GetPlantDiagnosisByIdAsync(request.DiagnosisId, cancellationToken);
        if (diagnosis == null || diagnosis.UserId != request.UserId)
        {
            throw new NotFoundException("Plant diagnosis", request.DiagnosisId);
        }

        if (!diagnosis.GardenPlantId.HasValue)
        {
            throw new ValidationException("This diagnosis is not linked to a garden plant.");
        }

        var dto = DiagnosisMapper.ToDto(diagnosis);
        var ai = dto.AiResult;

        var recoveryLines = new List<string>();
        if (!string.IsNullOrWhiteSpace(ai?.Disease))
        {
            recoveryLines.Add($"Active issue (photo diagnosis): {ai.Disease}");
        }

        if (ai?.Recommendations is { Count: > 0 })
        {
            recoveryLines.Add("Suggested actions: " + string.Join("; ", ai.Recommendations.Take(6)));
        }

        if (!string.IsNullOrWhiteSpace(ai?.Explanation))
        {
            recoveryLines.Add("Notes: " + ai.Explanation.Trim());
        }

        var plan = await _mediator.Send(new GenerateGardenPlantAiSchedulePlanQuery
        {
            UserId = request.UserId,
            PlantId = diagnosis.GardenPlantId.Value,
            HorizonDays = request.HorizonDays <= 0 ? 30 : request.HorizonDays,
            UtcOffsetMinutes = request.UtcOffsetMinutes,
            RecoveryDiagnosisContext = recoveryLines.Count > 0 ? string.Join(Environment.NewLine, recoveryLines) : null
        }, cancellationToken);

        return plan;
    }
}

