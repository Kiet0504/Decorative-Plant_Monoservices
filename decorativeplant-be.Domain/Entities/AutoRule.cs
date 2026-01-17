using System.Text.Json;

namespace decorativeplant_be.Domain.Entities;

public class AutoRule : BaseEntity
{
    public Guid DeviceId { get; set; }
    public string Name { get; set; } = string.Empty; // e.g., "Water when soil dry"
    public bool IsActive { get; set; } = true;
    public int Priority { get; set; }
    public JsonDocument? ConfigJson { get; set; } // Trigger conditions: { 'soil_moisture': { '<': 30 } }

    // Navigation properties
    public IotDevice IotDevice { get; set; } = null!;
    public ICollection<RuleExecutionLog> RuleExecutionLogs { get; set; } = new List<RuleExecutionLog>();
}
