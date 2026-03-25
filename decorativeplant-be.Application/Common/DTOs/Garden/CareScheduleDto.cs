namespace decorativeplant_be.Application.Common.DTOs.Garden;

public class CareScheduleDto
{
    public Guid Id { get; set; }

    public Guid? GardenPlantId { get; set; }

    public object? TaskInfo { get; set; }

    public bool IsActive { get; set; }
}

