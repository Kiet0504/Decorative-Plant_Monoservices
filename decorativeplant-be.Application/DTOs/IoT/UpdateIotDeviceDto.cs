using System.Text.Json;

namespace decorativeplant_be.Application.DTOs.IoT;

public class UpdateIotDeviceDto
{
    public Guid? BranchId { get; set; }
    public Guid? LocationId { get; set; }
    public JsonDocument? DeviceInfo { get; set; }
    public string? Status { get; set; }
    public JsonDocument? Components { get; set; }
}
