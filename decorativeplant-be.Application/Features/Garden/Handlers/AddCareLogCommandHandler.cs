using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Garden;
using decorativeplant_be.Application.Features.Garden.Commands;
using decorativeplant_be.Domain.Entities;
using MediatR;
using System.Text.Json;
using decorativeplant_be.Application.Common.DTOs.Garden;

namespace decorativeplant_be.Application.Features.Garden.Handlers;

public class AddCareLogCommandHandler : IRequestHandler<AddCareLogCommand, CareLogDto>
{
    private readonly IGardenRepository _gardenRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AddCareLogCommandHandler(
        IGardenRepository gardenRepository,
        IUnitOfWork unitOfWork)
    {
        _gardenRepository = gardenRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<CareLogDto> Handle(AddCareLogCommand request, CancellationToken cancellationToken)
    {
        var plant = await _gardenRepository.GetPlantByIdAsync(request.PlantId, includeTaxonomy: false, cancellationToken);
        if (plant == null || plant.UserId != request.UserId)
        {
            throw new NotFoundException("Garden plant", request.PlantId);
        }

        // Validation: if this care log is completing a scheduled task, don't allow completing it before it's due.
        // (Users can still log care actions without a ScheduleId anytime.)
        if (request.ScheduleId.HasValue)
        {
            var schedule = await _gardenRepository.GetScheduleByIdAsync(request.ScheduleId.Value, cancellationToken);
            if (schedule != null && schedule.GardenPlantId == request.PlantId && schedule.TaskInfo != null)
            {
                try
                {
                    var task = JsonSerializer.Deserialize<CareScheduleTaskInfoDto>(
                        schedule.TaskInfo.RootElement.GetRawText(),
                        TaskInfoJsonOptions);

                    var performedAt = request.PerformedAt ?? DateTime.UtcNow;
                    if (task?.NextDue != null)
                    {
                        // Small tolerance for clock drift / UI latency.
                        var tolerance = TimeSpan.FromMinutes(5);
                        if (performedAt + tolerance < task.NextDue.Value)
                        {
                            throw new ValidationException($"This task is not due yet. Due at {task.NextDue:O}.");
                        }
                    }
                }
                catch (ValidationException)
                {
                    throw;
                }
                catch
                {
                    // If task info cannot be parsed, don't block logging.
                }
            }
        }

        var actionType = request.ActionType.Trim();
        var mood = string.IsNullOrWhiteSpace(request.Mood) ? null : request.Mood.Trim();

        var logInfo = CareLogMapper.BuildLogInfoJson(
            actionType,
            request.Description,
            request.Products,
            request.Observations,
            mood);

        var images = CareLogMapper.BuildImagesJson(request.Images);

        var careLog = new CareLog
        {
            Id = Guid.NewGuid(),
            GardenPlantId = request.PlantId,
            ScheduleId = request.ScheduleId,
            LogInfo = logInfo,
            Images = images,
            PerformedAt = request.PerformedAt ?? DateTime.UtcNow
        };

        await _gardenRepository.AddCareLogAsync(careLog, cancellationToken);

        // Best-of-both behavior: if this log completes a scheduled task,
        // auto-advance schedule.next_due based on its frequency.
        if (request.ScheduleId.HasValue)
        {
            var schedule = await _gardenRepository.GetScheduleByIdAsync(request.ScheduleId.Value, cancellationToken);
            if (schedule != null && schedule.GardenPlantId == request.PlantId)
            {
                var updated = TryAdvanceSchedule(schedule, careLog.PerformedAt ?? DateTime.UtcNow);
                if (updated)
                {
                    await _gardenRepository.UpdateScheduleAsync(schedule, cancellationToken);
                }
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return CareLogMapper.ToDto(careLog);
    }

    private static readonly JsonSerializerOptions TaskInfoJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static bool TryAdvanceSchedule(CareSchedule schedule, DateTime performedAtUtc)
    {
        if (schedule.TaskInfo == null) return false;
        try
        {
            var task = JsonSerializer.Deserialize<CareScheduleTaskInfoDto>(
                schedule.TaskInfo.RootElement.GetRawText(),
                TaskInfoJsonOptions);
            if (task == null) return false;

            var days = task.IntervalDays.HasValue && task.IntervalDays.Value > 0
                ? task.IntervalDays.Value
                : ((task.Frequency ?? "").Trim().ToLowerInvariant()) switch
                {
                    "daily" => 1,
                    "every_2_3_days" => 3,
                    "weekly" => 7,
                    "biweekly" => 14,
                    "monthly" => 30,
                    "rarely" => 14,
                    _ => 7
                };

            // preserve time-of-day preference if present, otherwise keep performed time.
            var next = performedAtUtc.AddDays(days);
            if (!string.IsNullOrWhiteSpace(task.TimeOfDay))
            {
                var tod = task.TimeOfDay.Trim().ToLowerInvariant();
                next = tod switch
                {
                    "morning" => new DateTime(next.Year, next.Month, next.Day, 9, 0, 0, DateTimeKind.Utc),
                    "afternoon" => new DateTime(next.Year, next.Month, next.Day, 14, 0, 0, DateTimeKind.Utc),
                    "evening" => new DateTime(next.Year, next.Month, next.Day, 18, 0, 0, DateTimeKind.Utc),
                    _ => next
                };
            }

            task.NextDue = next;
            schedule.TaskInfo = CareScheduleMapper.BuildTaskInfoJson(task);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
