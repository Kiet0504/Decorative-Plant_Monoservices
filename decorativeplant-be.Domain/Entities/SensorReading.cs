namespace decorativeplant_be.Domain.Entities;

public class SensorReading : BaseEntity
{
    public Guid DeviceId { get; set; }
    public IoTDevice Device { get; set; } = null!;
    
    public string SensorType { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public string Unit { get; set; } = string.Empty;
    public DateTime RecordedAt { get; set; }
}
