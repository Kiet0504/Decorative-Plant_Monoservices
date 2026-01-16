namespace decorativeplant_be.Domain.Entities;

public class BatchLog : BaseEntity
{
    public Guid BatchId { get; set; }
    public PlantBatch Batch { get; set; } = null!;
    
    public string ActionType { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public DateTime PerformedAt { get; set; } = DateTime.UtcNow;
    public Guid PerformerId { get; set; }
    // Optional: Navigation property to Performer (UserAccount) if needed
    public UserAccount? Performer { get; set; }
}
