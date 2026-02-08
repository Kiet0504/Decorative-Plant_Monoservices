using System.Text.Json;

namespace decorativeplant_be.Domain.Entities;

/// <summary>
/// Automation rule. JSONB: schedule, conditions, actions. See JSONB_SCHEMA_REFERENCE.md
/// </summary>
public class AutomationRule
{
    public Guid Id { get; set; }
    public Guid? DeviceId { get; set; }
    public string? Name { get; set; }
    public int Priority { get; set; }
    public bool IsActive { get; set; }
    public JsonDocument? Schedule { get; set; }
    public JsonDocument? Conditions { get; set; }
    public JsonDocument? Actions { get; set; }
    public DateTime? CreatedAt { get; set; }

    public IotDevice? Device { get; set; }
    public ICollection<AutomationExecutionLog> ExecutionLogs { get; set; } = new List<AutomationExecutionLog>();
}
