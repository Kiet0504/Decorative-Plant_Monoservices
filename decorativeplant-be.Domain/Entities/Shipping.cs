using System.Text.Json;

namespace decorativeplant_be.Domain.Entities;

public class Shipping : BaseEntity
{
    public Guid OrderId { get; set; }
    public Guid PickupAddressId { get; set; }
    public Guid DeliveryAddressId { get; set; }
    public string Carrier { get; set; } = string.Empty; // GHN/GHTK
    public string? TrackingCode { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal ShippingFee { get; set; } // Actual fee paid to carrier
    public JsonDocument? EventsJson { get; set; } // Shipping history from webhook
    public DateTime? EstimatedDelivery { get; set; }

    // Navigation properties
    public OrderHeader OrderHeader { get; set; } = null!;
    public PickupAddressSnapshot PickupAddressSnapshot { get; set; } = null!;
    public ShippingAddressSnapshot ShippingAddressSnapshot { get; set; } = null!;
}
