using System.Text.Json;

namespace decorativeplant_be.Domain.Entities;

/// <summary>
/// IoT device. JSONB: device_info, activity_log, components. sensor_reading uses device_id + component_key.
/// See docs/JSONB_SCHEMA_REFERENCE.md
/// </summary>
public class IotDevice
{
    public Guid Id { get; set; }
    public Guid? BranchId { get; set; }
    public Guid? LocationId { get; set; }
    public JsonDocument? DeviceInfo { get; set; }
    public string? Status { get; set; }
    public JsonDocument? ActivityLog { get; set; }
    public JsonDocument? Components { get; set; }

    public Branch? Branch { get; set; }
    public InventoryLocation? Location { get; set; }
    public ICollection<SensorReading> SensorReadings { get; set; } = new List<SensorReading>();
    public ICollection<AutomationRule> AutomationRules { get; set; } = new List<AutomationRule>();
    public ICollection<IotAlert> IotAlerts { get; set; } = new List<IotAlert>();
}
