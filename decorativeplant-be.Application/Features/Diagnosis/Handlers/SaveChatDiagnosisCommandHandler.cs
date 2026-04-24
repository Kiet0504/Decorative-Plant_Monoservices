using decorativeplant_be.Application.Common.DTOs.Diagnosis;
using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Diagnosis.Commands;
using decorativeplant_be.Domain.Entities;
using MediatR;

namespace decorativeplant_be.Application.Features.Diagnosis.Handlers;

public sealed class SaveChatDiagnosisCommandHandler : IRequestHandler<SaveChatDiagnosisCommand, PlantDiagnosisDto>
{
    private readonly IGardenRepository _gardenRepository;
    private readonly IUnitOfWork _unitOfWork;

    public SaveChatDiagnosisCommandHandler(IGardenRepository gardenRepository, IUnitOfWork unitOfWork)
    {
        _gardenRepository = gardenRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<PlantDiagnosisDto> Handle(SaveChatDiagnosisCommand request, CancellationToken cancellationToken)
    {
        var plant = await _gardenRepository.GetPlantByIdAsync(request.GardenPlantId, includeTaxonomy: false, cancellationToken);
        if (plant == null || plant.UserId != request.UserId)
        {
            throw new NotFoundException("Garden plant", request.GardenPlantId);
        }

        var aiJson = DiagnosisMapper.BuildAiResultJsonFromSummaryDto(request.AiResult);
        var userInput = DiagnosisMapper.BuildUserInputJson(
            request.ImageUrl?.Trim() ?? string.Empty,
            "Saved from AI Hub chat diagnosis.");

        var diagnosis = new PlantDiagnosis
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            GardenPlantId = request.GardenPlantId,
            UserInput = userInput,
            AiResult = aiJson,
            Feedback = null,
            CreatedAt = DateTime.UtcNow,
            ResolvedAtUtc = null
        };

        await _gardenRepository.AddPlantDiagnosisAsync(diagnosis, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return DiagnosisMapper.ToDto(diagnosis);
    }
}
