using decorativeplant_be.Application.Common.DTOs.Garden;
using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Garden.Commands;
using decorativeplant_be.Domain.Entities;
using MediatR;
using System.Text.Json;

namespace decorativeplant_be.Application.Features.Garden.Handlers;

public sealed class BulkCreateCareSchedulesCommandHandler : IRequestHandler<BulkCreateCareSchedulesCommand, IReadOnlyList<CareScheduleDto>>
{
    private readonly IGardenRepository _gardenRepository;
    private readonly IUnitOfWork _unitOfWork;

    public BulkCreateCareSchedulesCommandHandler(IGardenRepository gardenRepository, IUnitOfWork unitOfWork)
    {
        _gardenRepository = gardenRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<CareScheduleDto>> Handle(BulkCreateCareSchedulesCommand request, CancellationToken cancellationToken)
    {
        var plant = await _gardenRepository.GetPlantByIdAsync(request.PlantId, includeTaxonomy: false, cancellationToken);
        if (plant == null || plant.UserId != request.UserId)
        {
            throw new NotFoundException("Garden plant", request.PlantId);
        }

        var tasks = request.Tasks ?? new List<CareScheduleTaskInfoDto>();
        if (tasks.Count == 0) return Array.Empty<CareScheduleDto>();

        // Production-safe: upsert by task type (avoid duplicate schedules when AI plan is saved repeatedly).
        var existing = (await _gardenRepository.GetSchedulesByPlantIdAsync(
            request.PlantId,
            includeInactive: true,
            cancellationToken)).ToList();

        var createdOrUpdated = new List<CareScheduleDto>(tasks.Count);

        foreach (var t in tasks)
        {
            var taskType = (t.Type ?? "").Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(taskType)) taskType = "water";

            var match = existing.FirstOrDefault(s =>
            {
                if (s.TaskInfo == null) return false;
                try
                {
                    var ti = JsonSerializer.Deserialize<CareScheduleTaskInfoDto>(
                        s.TaskInfo.RootElement.GetRawText(),
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    return ti != null && (ti.Type ?? "").Trim().ToLowerInvariant() == taskType;
                }
                catch
                {
                    return false;
                }
            });

            if (match != null)
            {
                // Update existing schedule: keep it active and replace task info
                match.IsActive = true;
                match.TaskInfo = CareScheduleMapper.BuildTaskInfoJson(t);
                await _gardenRepository.UpdateScheduleAsync(match, cancellationToken);
                createdOrUpdated.Add(CareScheduleMapper.ToDto(match));
            }
            else
            {
                var schedule = new CareSchedule
                {
                    Id = Guid.NewGuid(),
                    GardenPlantId = request.PlantId,
                    IsActive = true,
                    TaskInfo = CareScheduleMapper.BuildTaskInfoJson(t)
                };
                await _gardenRepository.AddScheduleAsync(schedule, cancellationToken);
                existing.Add(schedule);
                createdOrUpdated.Add(CareScheduleMapper.ToDto(schedule));
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return createdOrUpdated;
    }
}

