using System.Text.Json.Nodes;

namespace decorativeplant_be.Domain.Entities;

public class Notification : BaseEntity
{
    public Guid UserId { get; set; }
    public UserAccount User { get; set; } = null!;

    public string? Type { get; set; }
    public string? Title { get; set; }
    public string? Body { get; set; }
    public JsonNode? Data { get; set; }
    public bool IsRead { get; set; } = false;
    
    // CreatedAt inherited from BaseEntity
}
