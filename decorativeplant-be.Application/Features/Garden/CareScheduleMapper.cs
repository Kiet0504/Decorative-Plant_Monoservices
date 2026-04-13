using System.Text.Json;
using decorativeplant_be.Application.Common.DTOs.Garden;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Application.Features.Garden;

public static class CareScheduleMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static JsonDocument? BuildTaskInfoJson(CareScheduleTaskInfoDto task)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(task);
        return JsonDocument.Parse(json);
    }

    public static CareScheduleDto ToDto(CareSchedule s)
    {
        object? taskInfo = null;
        if (s.TaskInfo != null)
        {
            taskInfo = JsonSerializer.Deserialize<object>(s.TaskInfo.RootElement.GetRawText(), JsonOptions);
        }

        return new CareScheduleDto
        {
            Id = s.Id,
            GardenPlantId = s.GardenPlantId,
            TaskInfo = taskInfo,
            IsActive = s.IsActive
        };
    }
}

