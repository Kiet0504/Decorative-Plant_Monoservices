namespace decorativeplant_be.Domain.Entities;

public class SensorReading : BaseEntity
{
    public Guid DeviceId { get; set; }
    public string ComponentKey { get; set; } = string.Empty; // temperature / soil_ph
    public float Value { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public IotDevice IotDevice { get; set; } = null!;
}
