using System.Text.Json;
using decorativeplant_be.Application.Common.DTOs.Garden;
using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Garden.Queries;
using MediatR;

namespace decorativeplant_be.Application.Features.Garden.Handlers;

public class GetGardenPlantProfileQueryHandler : IRequestHandler<GetGardenPlantProfileQuery, PlantProfileDto>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IGardenRepository _gardenRepository;

    public GetGardenPlantProfileQueryHandler(IGardenRepository gardenRepository)
    {
        _gardenRepository = gardenRepository;
    }

    public async Task<PlantProfileDto> Handle(GetGardenPlantProfileQuery request, CancellationToken cancellationToken)
    {
        var plant = await _gardenRepository.GetPlantByIdAsync(request.PlantId, includeTaxonomy: true, cancellationToken);
        if (plant == null || plant.UserId != request.UserId)
        {
            throw new NotFoundException("Garden plant", request.PlantId);
        }

        var logs = await _gardenRepository.GetRecentCareLogsByPlantIdAsync(
            request.PlantId,
            request.RecentLogsLimit,
            cancellationToken);

        var schedules = await _gardenRepository.GetSchedulesByPlantIdAsync(
            request.PlantId,
            includeInactive: request.IncludeArchivedSchedules,
            cancellationToken);

        return new PlantProfileDto
        {
            Plant = GardenPlantMapper.ToDto(plant),
            RecentCareLogs = logs.Select(CareLogMapper.ToDto).ToList(),
            ActiveSchedules = schedules.Select(s => new CareScheduleDto
            {
                Id = s.Id,
                GardenPlantId = s.GardenPlantId,
                TaskInfo = s.TaskInfo == null ? null : JsonSerializer.Deserialize<object>(s.TaskInfo.RootElement.GetRawText(), JsonOptions),
                IsActive = s.IsActive
            }).ToList()
        };
    }
}

