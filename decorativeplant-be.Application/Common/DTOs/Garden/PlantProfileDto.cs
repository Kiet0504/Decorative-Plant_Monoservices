namespace decorativeplant_be.Application.Common.DTOs.Garden;

public class PlantProfileDto
{
    public GardenPlantDto Plant { get; set; } = new();

    public List<CareLogDto> RecentCareLogs { get; set; } = new();

    public List<CareScheduleDto> ActiveSchedules { get; set; } = new();
}

