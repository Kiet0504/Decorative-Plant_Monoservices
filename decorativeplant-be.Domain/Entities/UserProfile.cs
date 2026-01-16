using System.Text.Json.Nodes;

namespace decorativeplant_be.Domain.Entities;

public class UserProfile : BaseEntity
{
    public Guid UserId { get; set; }
    public UserAccount User { get; set; } = null!;
    
    public string DisplayName { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
    public JsonNode? AddressJson { get; set; }
    public JsonNode? PreferencesJson { get; set; }
    public string HardinessZone { get; set; } = string.Empty;
    public string ExperienceLevel { get; set; } = string.Empty;
}
