namespace decorativeplant_be.Application.Common.DTOs.Subscription;

// ── Request DTOs ──
public class CreateSubscriptionRequest
{
    /// <summary>Plan type: "Free" or "Premium". Defaults to "Free".</summary>
    public string PlanType { get; set; } = "Free";

    /// <summary>Payment method: "VNPay" | "MoMo" | "ZaloPay" | "Manual"</summary>
    public string? PaymentMethod { get; set; }

    /// <summary>Billing cycle: "Monthly" | "Yearly"</summary>
    public string? BillingCycle { get; set; }
}

public class CancelSubscriptionRequest
{
    /// <summary>Reason for cancellation</summary>
    public string Reason { get; set; } = string.Empty;
}

public class UpgradeSubscriptionRequest
{
    /// <summary>Payment method: "VNPay" | "MoMo" | "PayOS" | "ZaloPay" | "Manual"</summary>
    public string PaymentMethod { get; set; } = string.Empty;

    /// <summary>Billing cycle: "Monthly" | "Yearly"</summary>
    public string BillingCycle { get; set; } = string.Empty;

    /// <summary>Amount paid for the subscription (e.g., "199000")</summary>
    public string AmountPaid { get; set; } = string.Empty;
}

// ── Response DTOs ──
public class SubscriptionDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string PlanType { get; set; } = string.Empty; // "Free" | "Premium"
    public string Status { get; set; } = string.Empty; // "Active" | "Expired" | "Cancelled"
    public DateTime StartAt { get; set; }
    public DateTime? EndAt { get; set; }
    public bool AutoRenew { get; set; }
    public string? BillingCycle { get; set; }
    public int DaysRemaining { get; set; }
}
