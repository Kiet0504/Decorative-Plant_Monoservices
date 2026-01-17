using System.Text.Json;

namespace decorativeplant_be.Domain.Entities;

public class SellerPackage : BaseEntity
{
    public string Name { get; set; } = string.Empty; // Gold Shop / Posting Package 50
    public decimal Price { get; set; }
    public int DurationDays { get; set; }
    public JsonDocument? BenefitsJson { get; set; } // {max_products: 100, push_notif: true, fee_policy_override_id: uuid}
    public Guid? DefaultFeePolicyId { get; set; } // Link to Fee Policy

    // Navigation properties
    public PlatformFeePolicy? DefaultFeePolicy { get; set; }
    public ICollection<SellerSubscription> Subscriptions { get; set; } = new List<SellerSubscription>();
}
