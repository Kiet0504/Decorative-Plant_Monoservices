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

    public SubmitDiagnosisCommandHandler(
        IGardenRepository gardenRepository,
        IAiDiagnosisService aiDiagnosisService,
        IUnitOfWork unitOfWork)
    {
        _gardenRepository = gardenRepository;
        _aiDiagnosisService = aiDiagnosisService;
        _unitOfWork = unitOfWork;
    }

    public async Task<PlantDiagnosisDto> Handle(SubmitDiagnosisCommand request, CancellationToken cancellationToken)
    {
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
