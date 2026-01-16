using System.Text.Json.Nodes;

namespace decorativeplant_be.Domain.Entities;

public class RuleExecutionLog : BaseEntity
{
    public Guid RuleId { get; set; }
    public AutoRule Rule { get; set; } = null!;
    
    public DateTime ExecutedAt { get; set; }
    public string Result { get; set; } = string.Empty;
    public JsonNode? DetailsJson { get; set; }
}
