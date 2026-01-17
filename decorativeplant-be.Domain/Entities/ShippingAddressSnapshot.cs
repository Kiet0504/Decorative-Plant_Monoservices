using System.Text.Json;

namespace decorativeplant_be.Domain.Entities;

public class ShippingAddressSnapshot : BaseEntity
{
    public Guid OrderId { get; set; }
    public string RecipientName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string FullAddressText { get; set; } = string.Empty;
    public string? City { get; set; }
    public JsonDocument? Coordinates { get; set; } // {lat: float, long: float}

    // Navigation properties
    public OrderHeader OrderHeader { get; set; } = null!;
    public Shipping? Shipping { get; set; }
}
