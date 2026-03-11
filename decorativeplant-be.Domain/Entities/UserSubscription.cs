namespace decorativeplant_be.Domain.Entities;

/// <summary>
/// Tracks user subscription plans (Free or Premium).
/// Maps to table: user_subscription
/// </summary>
public class UserSubscription : BaseEntity
{
    public Guid UserId { get; set; }
    public string PlanType { get; set; } = string.Empty; // "Free" | "Premium" | "Premium 1" etc.
    public string Status { get; set; } = string.Empty; // "Active" | "Expired" | "Cancelled"
    public DateTime StartAt { get; set; }
    public DateTime? EndAt { get; set; }
    public bool AutoRenew { get; set; } = false;
    public string? PaymentMethod { get; set; } // "VNPay" | "MoMo" | "ZaloPay" | "PayOS" | "Manual"
    public string? AmountPaid { get; set; }
    public string? BillingCycle { get; set; } // "Monthly" | "Yearly"
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }

    // Navigation
    public UserAccount User { get; set; } = null!;
}
