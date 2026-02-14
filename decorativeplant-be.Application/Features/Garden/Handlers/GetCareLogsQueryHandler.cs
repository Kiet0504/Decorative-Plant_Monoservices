using decorativeplant_be.Application.Common.DTOs.Garden;
using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Garden;
using decorativeplant_be.Application.Features.Garden.Queries;
using MediatR;

namespace decorativeplant_be.Application.Features.Garden.Handlers;

public class GetCareLogsQueryHandler : IRequestHandler<GetCareLogsQuery, List<CareLogDto>>
{
    private readonly IGardenRepository _gardenRepository;

    public GetCareLogsQueryHandler(IGardenRepository gardenRepository)
    {
        _gardenRepository = gardenRepository;
    }

    public async Task<List<CareLogDto>> Handle(GetCareLogsQuery request, CancellationToken cancellationToken)
    {
        var plant = await _gardenRepository.GetPlantByIdAsync(request.PlantId, includeTaxonomy: false, cancellationToken);
        if (plant == null || plant.UserId != request.UserId)
        {
            throw new NotFoundException("Garden plant", request.PlantId);
        }

        var logs = await _gardenRepository.GetCareLogsByPlantIdAsync(request.PlantId, cancellationToken);
        return logs.Select(CareLogMapper.ToDto).ToList();
    }
}
