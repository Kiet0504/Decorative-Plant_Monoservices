namespace decorativeplant_be.Application.Common.DTOs.Commerce;

// ── Request DTOs ──
public class CreateOrderRequest
{
    public string OrderType { get; set; } = "online"; // online|offline
    public string FulfillmentMethod { get; set; } = "delivery"; // delivery|pickup
    public string? CustomerNote { get; set; }
    public string? VoucherCode { get; set; }
    public decimal ShippingFee { get; set; } // Total shipping fee from frontend
    // Delivery address (required if delivery)
    public DeliveryAddressDto? DeliveryAddress { get; set; }
    // Order items
    public List<CreateOrderItemRequest> Items { get; set; } = new();
}

public class CreateOrderItemRequest
{
    public Guid ListingId { get; set; }
    public int Quantity { get; set; }
}

public class UpdateOrderStatusRequest
{
    public string Status { get; set; } = string.Empty;
    public string? InternalNote { get; set; }
    public string? RejectionReason { get; set; }
    public string? TrackingCode { get; set; }
    public string? CarrierName { get; set; }
}

public class CancelOrderRequest
{
    public string? CancellationReason { get; set; }
}

// ── Response DTOs ──
public class OrderResponse
{
    public Guid Id { get; set; }
    public string? OrderCode { get; set; }
    public Guid? UserId { get; set; }
    public string? OrderType { get; set; }
    public string? FulfillmentMethod { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? TrackingCode { get; set; }
    public string? CarrierName { get; set; }
    public OrderFinancialsDto? Financials { get; set; }
    public DeliveryAddressDto? DeliveryAddress { get; set; }
    public string? CustomerNote { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public List<OrderItemResponse> Items { get; set; } = new();
}

public class OrderItemResponse
{
    public Guid Id { get; set; }
    public Guid? ListingId { get; set; }
    public Guid? StockId { get; set; }
    public Guid? BranchId { get; set; }
    public int Quantity { get; set; }
    public string? UnitPrice { get; set; }
    public string? Subtotal { get; set; }
    public string? TitleSnapshot { get; set; }
    public string? ImageSnapshot { get; set; }
}

public class OrderFinancialsDto
{
    public string Subtotal { get; set; } = "0";
    public string Shipping { get; set; } = "0";
    public string Discount { get; set; } = "0";
    public string Tax { get; set; } = "0";
    public string Total { get; set; } = "0";
}

public class DeliveryAddressDto
{
    public string RecipientName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string AddressLine1 { get; set; } = string.Empty;
    public string? City { get; set; }
    public int DistrictId { get; set; }
    public string WardCode { get; set; } = string.Empty;
}
