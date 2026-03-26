using decorativeplant_be.Application.Common.DTOs.Garden;
using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Garden.Queries;
using MediatR;

namespace decorativeplant_be.Application.Features.Garden.Handlers;

public class GetGrowthGalleryQueryHandler : IRequestHandler<GetGrowthGalleryQuery, GrowthTimelineDto>
{
    private readonly IGardenRepository _gardenRepository;

    public GetGrowthGalleryQueryHandler(IGardenRepository gardenRepository)
    {
        _gardenRepository = gardenRepository;
    }

    public async Task<GrowthTimelineDto> Handle(GetGrowthGalleryQuery request, CancellationToken cancellationToken)
    {
        var plant = await _gardenRepository.GetPlantByIdAsync(request.PlantId, includeTaxonomy: false, cancellationToken);
        if (plant == null || plant.UserId != request.UserId)
        {
            throw new NotFoundException("Garden plant", request.PlantId);
        }

        var (items, totalCount) = await _gardenRepository.GetPhotoCareLogsByPlantIdAsync(
            request.PlantId,
            request.Before,
            request.Limit,
            cancellationToken);

        // We fetched newest-first for pagination; UI wants chronological order.
        var logs = items.OrderBy(l => l.PerformedAt ?? DateTime.MinValue).ToList();

        var details = GardenPlantMapper.DeserializeDetails(plant.Details);
        var header = new GrowthTimelineHeaderDto
        {
            GardenPlantId = plant.Id,
            PlantNickname = details.Nickname,
            AdoptedDate = details.AdoptedDate,
            CoverImageUrl = plant.ImageUrl,
            TotalCount = totalCount
        };

        var entries = logs
            .Select(CareLogMapper.ToDto)
            .Select(dto =>
            {
                var firstImage = dto.Images?.FirstOrDefault();
                return new GrowthPhotoEntryDto
                {
                    CareLogId = dto.Id,
                    ImageUrl = firstImage?.Url ?? string.Empty,
                    Caption = firstImage?.Caption ?? dto.LogInfo?.Description,
                    PerformedAt = dto.PerformedAt
                };
            })
            .Where(e => !string.IsNullOrWhiteSpace(e.ImageUrl))
            .ToList();

        var nextCursor = items.OrderByDescending(i => i.PerformedAt ?? DateTime.MinValue).FirstOrDefault()?.PerformedAt;

        return new GrowthTimelineDto
        {
            Header = header,
            Entries = entries,
            NextCursor = nextCursor?.ToUniversalTime().ToString("O")
        };
    }
}

