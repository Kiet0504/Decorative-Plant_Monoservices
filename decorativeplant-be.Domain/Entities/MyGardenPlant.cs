namespace decorativeplant_be.Domain.Entities;

public class MyGardenPlant : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid SourceOrderItemId { get; set; } // Link back to original Batch
    public Guid TaxonomyId { get; set; }
    public string? Nickname { get; set; }
    public DateTime? AdoptedDate { get; set; }
    public string? HealthStatus { get; set; }
    public string? ImageUrl { get; set; }

    // Navigation properties
    public UserAccount UserAccount { get; set; } = null!;
    public OrderItem SourceOrderItem { get; set; } = null!;
    public PlantTaxonomy PlantTaxonomy { get; set; } = null!;
    public ICollection<CareSchedule> CareSchedules { get; set; } = new List<CareSchedule>();
    public ICollection<CareLog> CareLogs { get; set; } = new List<CareLog>();
    public ICollection<DiagnosisLog> DiagnosisLogs { get; set; } = new List<DiagnosisLog>();
}
