namespace decorativeplant_be.Domain.Entities;

public class CareSchedule : BaseEntity
{
    public Guid GardenPlantId { get; set; }
    public string TaskType { get; set; } = string.Empty; // Water/Fertilize
    public string Frequency { get; set; } = string.Empty; // Daily/Weekly
    public DateTime? NextDueDate { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public MyGardenPlant MyGardenPlant { get; set; } = null!;
}
