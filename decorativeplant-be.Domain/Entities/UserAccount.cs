using System.Text.Json;

namespace decorativeplant_be.Domain.Entities;

public class UserAccount : BaseEntity
{
    public string Email { get; set; } = string.Empty;
    public string? PasswordHash { get; set; }
    public string? Phone { get; set; }
    public string Role { get; set; } = string.Empty; // admin, branch_manager, store_staff, cultivation_staff, fulfillment_staff_customer
    public bool IsActive { get; set; } = true;
    public bool EmailVerified { get; set; } = false;

    // Merged user_profile
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
    public string? Bio { get; set; }
    public string? LocationCity { get; set; }
    public string? HardinessZone { get; set; }
    public string? ExperienceLevel { get; set; }

    // === AI Consultation Profile ===
    /// <summary>
    /// Values: 'low_light' | 'indirect' | 'filtered' | 'morning_3h' | 'full_sun_6h'
    /// Maps to dataset column: Sunlight_Exposure
    /// </summary>
    public string? SunlightExposure { get; set; }

    /// <summary>
    /// Values: 'cool' (15–20°C) | 'moderate' (20–25°C) | 'warm' (25–30°C)
    /// Maps to dataset column: Room_Temperature_C
    /// </summary>
    public string? RoomTemperatureRange { get; set; }

    /// <summary>
    /// Values: 'dry' | 'moderate' | 'humid'
    /// Maps to dataset column: Humidity_%
    /// </summary>
    public string? HumidityLevel { get; set; }

    /// <summary>
    /// Values: 'daily' | 'every_2_3_days' | 'weekly' | 'rarely'
    /// Maps to dataset column: Watering_Frequency_days
    /// </summary>
    public string? WateringFrequency { get; set; }

    /// <summary>
    /// Values: 'desk' | 'living_room' | 'bedroom' | 'hallway' | 'office' | 'balcony'
    /// </summary>
    public string? PlacementLocation { get; set; }

    /// <summary>
    /// Values: 'small' | 'medium' | 'large'
    /// </summary>
    public string? SpaceSize { get; set; }

    public bool? HasChildrenOrPets { get; set; }

    /// <summary>
    /// Example: ["decoration", "air_purification", "easy_care", "flowering"]
    /// Follow the same JsonDocument pattern used by the Addresses field.
    /// </summary>
    public JsonDocument? PlantGoals { get; set; }

    /// <summary>
    /// Values: 'tropical' | 'minimalist' | 'classic'
    /// </summary>
    public string? PreferredStyle { get; set; }

    /// <summary>
    /// Budget range.
    /// Values: 'low' | 'medium' | 'unlimited'
    /// </summary>
    public string? BudgetRange { get; set; }

    /// <summary>
    /// Frontend should redirect to /onboarding if this is false after login.
    /// </summary>
    public bool IsProfileCompleted { get; set; } = false;

    /// <summary>Array of address objects. See JSONB_SCHEMA_REFERENCE.md</summary>
    public JsonDocument? Addresses { get; set; }

    public DateTime? LastLoginAt { get; set; }

    // Navigation
    public Guid? CompanyId { get; set; }
    public Company? Company { get; set; }

    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    public ICollection<StaffAssignment> StaffAssignments { get; set; } = new List<StaffAssignment>();
    public ICollection<ShoppingCart> ShoppingCarts { get; set; } = new List<ShoppingCart>();
    public ICollection<OrderHeader> Orders { get; set; } = new List<OrderHeader>();
    public ICollection<ProductReview> ProductReviews { get; set; } = new List<ProductReview>();
    public ICollection<WishlistItem> WishlistItems { get; set; } = new List<WishlistItem>();
    public ICollection<GardenPlant> GardenPlants { get; set; } = new List<GardenPlant>();
    public ICollection<CultivationLog> CultivationLogs { get; set; } = new List<CultivationLog>();
    public ICollection<ReturnRequest> ReturnRequests { get; set; } = new List<ReturnRequest>();
    public ICollection<AiTrainingFeedback> AiTrainingFeedbacks { get; set; } = new List<AiTrainingFeedback>();
}
