using System.Text.Json;

namespace decorativeplant_be.Application.DTOs.IoT;

public class IotAlertDto
{
    public Guid Id { get; set; }
    public Guid? DeviceId { get; set; }
    public string? ComponentKey { get; set; }
    public JsonDocument? AlertInfo { get; set; }
    public JsonDocument? ResolutionInfo { get; set; }
    public Guid? BranchId { get; set; }
    public string? BranchName { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public class ResolveIotAlertDto
{
    public JsonDocument? ResolutionInfo { get; set; }
}
