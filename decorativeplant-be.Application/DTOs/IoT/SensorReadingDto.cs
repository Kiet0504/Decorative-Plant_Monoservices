using System.Text.Json;

namespace decorativeplant_be.Application.DTOs.IoT;

public class SensorReadingDto
{
    public Guid Id { get; set; }
    public Guid DeviceId { get; set; }
    public string ComponentKey { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public DateTime Timestamp { get; set; }
}
