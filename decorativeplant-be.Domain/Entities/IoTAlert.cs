using System.Text.Json.Nodes;

namespace decorativeplant_be.Domain.Entities;

public class IoTAlert : BaseEntity
{
    public Guid DeviceId { get; set; }
    public IoTDevice Device { get; set; } = null!;
    
    public string? ComponentKey { get; set; }
    public JsonNode? AlertInfo { get; set; }
    public JsonNode? ResolutionInfo { get; set; }
}
