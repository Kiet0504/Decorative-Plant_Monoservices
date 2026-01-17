using System.Text.Json;

namespace decorativeplant_be.Domain.Entities;

public class Notification : BaseEntity
{
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // Order/CareReminder/System
    public JsonDocument? PayloadJson { get; set; } // Data for app navigation
    public bool IsRead { get; set; } = false;
    public DateTime? ScheduledAt { get; set; }

    // Navigation properties
    public UserAccount UserAccount { get; set; } = null!;
}
