using System.Text.Json;

namespace decorativeplant_be.Domain.Entities;

/// <summary>
/// Order header. JSONB: type_info, financials, notes, delivery_address, pickup_info.
/// See docs/JSONB_SCHEMA_REFERENCE.md
/// </summary>
public class OrderHeader
{
    public Guid Id { get; set; }
    public string? OrderCode { get; set; }
    public Guid? UserId { get; set; }
    public Guid? VoucherId { get; set; }

    public JsonDocument? TypeInfo { get; set; }
    public JsonDocument? Financials { get; set; }
    public string? Status { get; set; }
    public JsonDocument? Notes { get; set; }
    public JsonDocument? DeliveryAddress { get; set; }
    public JsonDocument? PickupInfo { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }

    /// <summary>Staff assigned to fulfill this order. Null = queued (no capacity available).</summary>
    public Guid? AssignedStaffId { get; set; }

    public UserAccount? User { get; set; }
    public UserAccount? AssignedStaff { get; set; }
    public Voucher? Voucher { get; set; }

    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    public ICollection<PaymentTransaction> PaymentTransactions { get; set; } = new List<PaymentTransaction>();
    public ICollection<Shipping> Shippings { get; set; } = new List<Shipping>();
    public ICollection<ReturnRequest> ReturnRequests { get; set; } = new List<ReturnRequest>();
}
