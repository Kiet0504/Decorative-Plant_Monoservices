using decorativeplant_be.Application.Common.DTOs.Garden;
using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Garden;
using decorativeplant_be.Application.Features.Garden.Queries;
using MediatR;

namespace decorativeplant_be.Application.Features.Garden.Handlers;

public class GetCareLogQueryHandler : IRequestHandler<GetCareLogQuery, CareLogDto>
{
    private readonly IGardenRepository _gardenRepository;

    public GetCareLogQueryHandler(IGardenRepository gardenRepository)
    {
        _gardenRepository = gardenRepository;
    }

    public async Task<CareLogDto> Handle(GetCareLogQuery request, CancellationToken cancellationToken)
    {
        var log = await _gardenRepository.GetCareLogByIdAsync(request.LogId, cancellationToken);
        if (log == null || log.GardenPlantId != request.PlantId)
        {
            throw new NotFoundException("Care log", request.LogId);
        }

        var plant = await _gardenRepository.GetPlantByIdAsync(request.PlantId, includeTaxonomy: false, cancellationToken);
        if (plant == null || plant.UserId != request.UserId)
        {
            throw new NotFoundException("Garden plant", request.PlantId);
        }

        return CareLogMapper.ToDto(log);
    }
}
