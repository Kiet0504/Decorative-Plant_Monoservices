using decorativeplant_be.Application.Common.DTOs.Diagnosis;
using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Diagnosis.Commands;
using decorativeplant_be.Domain.Entities;
using MediatR;

namespace decorativeplant_be.Application.Features.Diagnosis.Handlers;

public class SubmitDiagnosisCommandHandler : IRequestHandler<SubmitDiagnosisCommand, PlantDiagnosisDto>
{
    private readonly IGardenRepository _gardenRepository;
    private readonly IAiDiagnosisService _aiDiagnosisService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserContentSafetyService _contentSafety;
    private readonly IPlantAssistantScopeService _plantScope;

    public SubmitDiagnosisCommandHandler(
        IGardenRepository gardenRepository,
        IAiDiagnosisService aiDiagnosisService,
        IUnitOfWork unitOfWork,
        IUserContentSafetyService contentSafety,
        IPlantAssistantScopeService plantScope)
    {
        _gardenRepository = gardenRepository;
        _aiDiagnosisService = aiDiagnosisService;
        _unitOfWork = unitOfWork;
        _contentSafety = contentSafety;
        _plantScope = plantScope;
    }

    public async Task<PlantDiagnosisDto> Handle(SubmitDiagnosisCommand request, CancellationToken cancellationToken)
    {
        if (!_contentSafety.IsAllowed(request.UserDescription))
        {
            throw new ValidationException(_contentSafety.BlockedApiMessage);
        }

        if (!string.IsNullOrWhiteSpace(request.UserDescription) &&
            !_plantScope.IsInScopeForPlainUserText(request.UserDescription))
        {
            throw new ValidationException(_plantScope.OutOfScopeApiMessage);
        }

        if (request.GardenPlantId.HasValue)
        {
            var plant = await _gardenRepository.GetPlantByIdAsync(request.GardenPlantId.Value, includeTaxonomy: false, cancellationToken);
            if (plant == null || plant.UserId != request.UserId)
            {
                throw new NotFoundException("Garden plant", request.GardenPlantId.Value);
            }
        }

        var aiResult = await _aiDiagnosisService.AnalyzePlantImageAsync(
            request.ImageUrl,
            request.UserDescription,
            cancellationToken);

        var userInput = DiagnosisMapper.BuildUserInputJson(request.ImageUrl, request.UserDescription);
        var aiResultJson = DiagnosisMapper.BuildAiResultJson(aiResult);

        var diagnosis = new PlantDiagnosis
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            GardenPlantId = request.GardenPlantId,
            UserInput = userInput,
            AiResult = aiResultJson,
            Feedback = null,
            CreatedAt = DateTime.UtcNow
        };

        await _gardenRepository.AddPlantDiagnosisAsync(diagnosis, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return DiagnosisMapper.ToDto(diagnosis);
    }
}
