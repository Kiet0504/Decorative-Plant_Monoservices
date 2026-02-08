using System.Text.Json;

namespace decorativeplant_be.Domain.Entities;

/// <summary>
/// Shipping record. JSONB: carrier_info, delivery_details, events. See JSONB_SCHEMA_REFERENCE.md
/// </summary>
public class Shipping
{
    public Guid Id { get; set; }
    public Guid? OrderId { get; set; }
    public string? TrackingCode { get; set; }
    public JsonDocument? CarrierInfo { get; set; }
    public string? Status { get; set; }
    public JsonDocument? DeliveryDetails { get; set; }
    public JsonDocument? Events { get; set; }

    public OrderHeader? Order { get; set; }
}
