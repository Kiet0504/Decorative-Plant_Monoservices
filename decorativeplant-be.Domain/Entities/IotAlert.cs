using System.Text.Json;

namespace decorativeplant_be.Domain.Entities;

/// <summary>
/// IoT alert. JSONB: alert_info, resolution_info. See JSONB_SCHEMA_REFERENCE.md
/// </summary>
public class IotAlert
{
    public Guid Id { get; set; }
    public Guid? DeviceId { get; set; }
    public string? ComponentKey { get; set; }
    public JsonDocument? AlertInfo { get; set; }
    public JsonDocument? ResolutionInfo { get; set; }
    public DateTime? CreatedAt { get; set; }

    public IotDevice? Device { get; set; }
}
