using System.Text.Json;

namespace decorativeplant_be.Domain.Entities;

public class UserProfile
{
    public Guid UserId { get; set; }
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
    public JsonDocument? AddressJson { get; set; } // List of shipping addresses
    public JsonDocument? PreferencesJson { get; set; } // Plant preferences, push token, etc.
    public string? HardinessZone { get; set; }
    public string? ExperienceLevel { get; set; } // Beginner/Expert
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public UserAccount UserAccount { get; set; } = null!;
}
