namespace decorativeplant_be.Domain.Entities;

public sealed class AiChatThread
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string? Title { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public UserAccount? User { get; set; }
    public ICollection<AiChatMessage> Messages { get; set; } = new List<AiChatMessage>();
}

