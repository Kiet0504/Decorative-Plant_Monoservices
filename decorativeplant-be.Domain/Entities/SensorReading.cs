namespace decorativeplant_be.Domain.Entities;

public class SensorReading : BaseEntity
{
    public Guid DeviceId { get; set; }
    public IoTDevice Device { get; set; } = null!;
    
    public string ComponentKey { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public DateTime RecordedAt { get; set; }
}
