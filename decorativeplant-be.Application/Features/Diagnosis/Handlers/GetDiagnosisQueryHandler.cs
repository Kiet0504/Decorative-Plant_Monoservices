using decorativeplant_be.Application.Common.DTOs.Diagnosis;
using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Diagnosis.Queries;
using MediatR;

namespace decorativeplant_be.Application.Features.Diagnosis.Handlers;

public class GetDiagnosisQueryHandler : IRequestHandler<GetDiagnosisQuery, PlantDiagnosisDto?>
{
    private readonly IGardenRepository _gardenRepository;

    public GetDiagnosisQueryHandler(IGardenRepository gardenRepository)
    {
        _gardenRepository = gardenRepository;
    }

    public async Task<PlantDiagnosisDto?> Handle(GetDiagnosisQuery request, CancellationToken cancellationToken)
    {
        var diagnosis = await _gardenRepository.GetPlantDiagnosisByIdAsync(request.Id, cancellationToken);
        if (diagnosis == null)
        {
            return null;
        }

        var isOwner = diagnosis.UserId == request.UserId ||
            (diagnosis.GardenPlant != null && diagnosis.GardenPlant.UserId == request.UserId);
        if (!isOwner)
        {
            throw new NotFoundException("Diagnosis", request.Id);
        }

        return DiagnosisMapper.ToDto(diagnosis);
    }
}
