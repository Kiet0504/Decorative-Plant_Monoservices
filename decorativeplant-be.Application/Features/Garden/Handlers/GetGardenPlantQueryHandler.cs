using decorativeplant_be.Application.Common.DTOs.Garden;
using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Garden;
using decorativeplant_be.Application.Features.Garden.Queries;
using MediatR;

namespace decorativeplant_be.Application.Features.Garden.Handlers;

public class GetGardenPlantQueryHandler : IRequestHandler<GetGardenPlantQuery, GardenPlantDto>
{
    private readonly IGardenRepository _gardenRepository;

    public GetGardenPlantQueryHandler(IGardenRepository gardenRepository)
    {
        _gardenRepository = gardenRepository;
    }

    public async Task<GardenPlantDto> Handle(GetGardenPlantQuery request, CancellationToken cancellationToken)
    {
        var plant = await _gardenRepository.GetPlantByIdAsync(request.Id, includeTaxonomy: true, cancellationToken);
        if (plant == null || plant.UserId != request.UserId)
        {
            throw new NotFoundException("Garden plant", request.Id);
        }

        return GardenPlantMapper.ToDto(plant);
    }
}
