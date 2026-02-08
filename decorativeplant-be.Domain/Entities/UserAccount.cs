using System.Text.Json;

namespace decorativeplant_be.Domain.Entities;

/// <summary>
/// Centralized user account (staff + customers). Profile and addresses merged.
/// JSONB: addresses — see docs/JSONB_SCHEMA_REFERENCE.md § user_account.addresses
/// </summary>
public class UserAccount : BaseEntity
{
    public string Email { get; set; } = string.Empty;
    public string? PasswordHash { get; set; }
    public string? Phone { get; set; }
    public string Role { get; set; } = string.Empty; // super_admin, branch_manager, staff, customer
    public bool IsActive { get; set; } = true;
    public bool EmailVerified { get; set; } = false;

    // Merged user_profile
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
    public string? Bio { get; set; }
    public string? LocationCity { get; set; }
    public string? HardinessZone { get; set; }
    public string? ExperienceLevel { get; set; }

    /// <summary>Array of address objects. See JSONB_SCHEMA_REFERENCE.md</summary>
    public JsonDocument? Addresses { get; set; }

    public DateTime? LastLoginAt { get; set; }

    // Navigation
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    public ICollection<StaffAssignment> StaffAssignments { get; set; } = new List<StaffAssignment>();
    public ICollection<ShoppingCart> ShoppingCarts { get; set; } = new List<ShoppingCart>();
    public ICollection<OrderHeader> Orders { get; set; } = new List<OrderHeader>();
    public ICollection<ProductReview> ProductReviews { get; set; } = new List<ProductReview>();
    public ICollection<GardenPlant> GardenPlants { get; set; } = new List<GardenPlant>();
    public ICollection<CultivationLog> CultivationLogs { get; set; } = new List<CultivationLog>();
    public ICollection<ReturnRequest> ReturnRequests { get; set; } = new List<ReturnRequest>();
    public ICollection<AiTrainingFeedback> AiTrainingFeedbacks { get; set; } = new List<AiTrainingFeedback>();
}
