namespace decorativeplant_be.Domain.Entities;

public class RuleExecutionLog : BaseEntity
{
    public Guid RuleId { get; set; }
    public string Result { get; set; } = string.Empty; // Success/Failed
    public DateTime TriggeredAt { get; set; } = DateTime.UtcNow;
    public string? Message { get; set; }

    // Navigation properties
    public AutoRule AutoRule { get; set; } = null!;
}
