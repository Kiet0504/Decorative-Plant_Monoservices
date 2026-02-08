namespace decorativeplant_be.Domain.Entities;

/// <summary>
/// Time-series sensor reading. component_key references key in iot_device.components JSONB.
/// </summary>
public class SensorReading
{
    public Guid Id { get; set; }
    public Guid DeviceId { get; set; }
    public string? ComponentKey { get; set; }
    public decimal Value { get; set; }
    public DateTime? RecordedAt { get; set; }

    public IotDevice Device { get; set; } = null!;
}
