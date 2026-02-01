using System.Text.Json.Nodes;

namespace decorativeplant_be.Domain.Entities;

public class CareLog : BaseEntity
{
    public Guid GardenPlantId { get; set; }
    public GardenPlant GardenPlant { get; set; } = null!;
    
    public Guid? ScheduleId { get; set; }
    public CareSchedule? Schedule { get; set; }
    
    public JsonNode? LogInfo { get; set; }
    public JsonNode? Images { get; set; }
    
    public DateTime PerformedAt { get; set; }
}
