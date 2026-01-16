using System.Text.Json.Nodes;

namespace decorativeplant_be.Domain.Entities;

public class Notification : BaseEntity
{
    public Guid UserId { get; set; }
    public UserAccount User { get; set; } = null!;
    
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public string ReferenceType { get; set; } = string.Empty; // e.g., ORDER, CARE_REMINDER
    public Guid? ReferenceId { get; set; }
    public JsonNode? PayloadJson { get; set; }
}
