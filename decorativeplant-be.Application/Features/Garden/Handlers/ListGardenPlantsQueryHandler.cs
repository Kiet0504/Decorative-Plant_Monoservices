using decorativeplant_be.Application.Common.DTOs.Garden;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Garden;
using decorativeplant_be.Application.Features.Garden.Queries;
using MediatR;

namespace decorativeplant_be.Application.Features.Garden.Handlers;

public class ListGardenPlantsQueryHandler : IRequestHandler<ListGardenPlantsQuery, PagedResultDto<GardenPlantDto>>
{
    private readonly IGardenRepository _gardenRepository;

    public ListGardenPlantsQueryHandler(IGardenRepository gardenRepository)
    {
        _gardenRepository = gardenRepository;
    }

    public async Task<PagedResultDto<GardenPlantDto>> Handle(ListGardenPlantsQuery request, CancellationToken cancellationToken)
    {
        var pageSize = Math.Clamp(request.PageSize, 1, 50);
        var page = Math.Max(1, request.Page);

        var (items, totalCount) = await _gardenRepository.GetPlantsByUserIdAsync(
            request.UserId,
            includeArchived: request.IncludeArchived,
            healthFilter: request.HealthFilter,
            page,
            pageSize,
            cancellationToken);

        var dtos = items.Select(GardenPlantMapper.ToDto).ToList();

        return new PagedResultDto<GardenPlantDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }
}
