using System.Text.Json;

namespace decorativeplant_be.Domain.Entities;

/// <summary>
/// Automation execution log. JSONB: execution_info. See JSONB_SCHEMA_REFERENCE.md
/// </summary>
public class AutomationExecutionLog
{
    public Guid Id { get; set; }
    public Guid? RuleId { get; set; }
    public JsonDocument? ExecutionInfo { get; set; }
    public DateTime? ExecutedAt { get; set; }

    public AutomationRule? Rule { get; set; }
}
