using System.Text.Json.Nodes;

namespace decorativeplant_be.Domain.Entities;

public class IoTDevice : BaseEntity
{
    public Guid UserId { get; set; }
    public UserAccount User { get; set; } = null!;
    
    public string DeviceName { get; set; } = string.Empty;
    public string DeviceType { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;
    public string FirmwareVersion { get; set; } = string.Empty;
    public bool IsOnline { get; set; }
    public JsonNode? ConfigJson { get; set; }
    public DateTime LastSeen { get; set; }
}
