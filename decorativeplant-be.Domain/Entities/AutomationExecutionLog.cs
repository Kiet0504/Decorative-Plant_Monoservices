using System.Text.Json.Nodes;

namespace decorativeplant_be.Domain.Entities;

public class AutomationExecutionLog : BaseEntity
{
    public Guid RuleId { get; set; }
    public AutomationRule Rule { get; set; } = null!;
    
    public JsonNode? ExecutionInfo { get; set; }
    public DateTime ExecutedAt { get; set; }
}
