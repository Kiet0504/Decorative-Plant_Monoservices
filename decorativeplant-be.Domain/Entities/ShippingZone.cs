using System.Text.Json;

namespace decorativeplant_be.Domain.Entities;

/// <summary>
/// Shipping zone for branch. JSONB: locations, fee_config, delivery_time_config.
/// </summary>
public class ShippingZone
{
    public Guid Id { get; set; }
    public Guid? BranchId { get; set; }
    public string? Name { get; set; }
    public JsonDocument? Locations { get; set; }
    public JsonDocument? FeeConfig { get; set; }
    public JsonDocument? DeliveryTimeConfig { get; set; }

    public Branch? Branch { get; set; }
}
