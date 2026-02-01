using Microsoft.AspNetCore.Identity;
using System.Text.Json.Nodes;

namespace decorativeplant_be.Domain.Entities;

public class UserAddress
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Label { get; set; } = string.Empty;
    public string RecipientName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string AddressLine1 { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
}

public class UserAccount : IdentityUser<Guid>
{
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    
    // Merged user_profile
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
    public string? Bio { get; set; }
    public string? LocationCity { get; set; }
    public string? HardinessZone { get; set; }
    public string? ExperienceLevel { get; set; }

    // Merged user_address (Array of objects)
    public List<UserAddress> Addresses { get; set; } = new();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}
