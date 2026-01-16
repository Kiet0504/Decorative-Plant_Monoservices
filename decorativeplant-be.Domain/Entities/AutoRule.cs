using System.Text.Json.Nodes;

namespace decorativeplant_be.Domain.Entities;

public class AutoRule : BaseEntity
{
    public Guid DeviceId { get; set; }
    public IoTDevice Device { get; set; } = null!;
    
    public string RuleName { get; set; } = string.Empty;
    public string ConditionType { get; set; } = string.Empty;
    public decimal ThresholdValue { get; set; }
    public string Operator { get; set; } = string.Empty; // >, <, =
    public string ActionType { get; set; } = string.Empty;
    public JsonNode? ActionPayloadJson { get; set; }
    public bool IsEnabled { get; set; }
}
