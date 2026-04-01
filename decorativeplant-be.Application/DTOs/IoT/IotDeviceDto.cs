using System.Text.Json;

namespace decorativeplant_be.Application.DTOs.IoT;

public class IotDeviceDto
{
    public Guid Id { get; set; }
    public Guid? BranchId { get; set; }
    public Guid? LocationId { get; set; }
    public string SecretKey { get; set; } = string.Empty;
    public JsonDocument? DeviceInfo { get; set; }
    public string? Name { get; set; }
    public string? Type { get; set; }
    public string? Status { get; set; }
    public string? LocationName { get; set; }
    public JsonDocument? ActivityLog { get; set; }
    public JsonDocument? Components { get; set; }
    public IEnumerable<SensorReadingDto>? LatestReadings { get; set; }
}
