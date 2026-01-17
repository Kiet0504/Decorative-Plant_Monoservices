namespace decorativeplant_be.Domain.Entities;

public class BatchLog : BaseEntity
{
    public Guid BatchId { get; set; }
    public string ActionType { get; set; } = string.Empty; // Water/Fertilize/Prune/Move
    public string? Notes { get; set; }
    public DateTime PerformedAt { get; set; } = DateTime.UtcNow;
    public Guid PerformerId { get; set; }

    // Navigation properties
    public PlantBatch PlantBatch { get; set; } = null!;
    public UserAccount Performer { get; set; } = null!;
}
