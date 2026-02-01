using System.Text.Json.Nodes;

namespace decorativeplant_be.Domain.Entities;

public class AutomationRule : BaseEntity
{
    public Guid DeviceId { get; set; }
    public IoTDevice Device { get; set; } = null!;
    
    public string Name { get; set; } = string.Empty;
    public int Priority { get; set; }
    public bool IsActive { get; set; }
    
    public JsonNode? Schedule { get; set; }
    public JsonNode? Conditions { get; set; }
    public JsonNode? Actions { get; set; }
}
