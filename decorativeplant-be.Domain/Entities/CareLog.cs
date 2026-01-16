using System.Text.Json.Nodes;

namespace decorativeplant_be.Domain.Entities;

public class CareLog : BaseEntity
{
    public Guid ScheduleId { get; set; }
    // Optional navigation if strict FK exists, assuming yes for now.
    public CareSchedule? Schedule { get; set; }
    
    public DateTime ActionDate { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public JsonNode? ImagesJson { get; set; } // e.g. ["url1", "url2"]
    public string Notes { get; set; } = string.Empty;
}
