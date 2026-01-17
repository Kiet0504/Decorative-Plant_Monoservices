namespace decorativeplant_be.Domain.Entities;

public class PlantBatch : BaseEntity
{
    public Guid StoreId { get; set; }
    public Guid TaxonomyId { get; set; }
    public Guid? ParentBatchId { get; set; } // If propagated from old batch
    public string BatchCode { get; set; } = string.Empty; // Internal management code
    public DateTime? SowingDate { get; set; } // Seed sowing/cutting date
    public string? SourceType { get; set; } // Seed/Cutting/Import

    // Navigation properties
    public Store Store { get; set; } = null!;
    public PlantTaxonomy PlantTaxonomy { get; set; } = null!;
    public PlantBatch? ParentBatch { get; set; }
    public ICollection<PlantBatch> ChildBatches { get; set; } = new List<PlantBatch>();
    public ICollection<BatchStock> BatchStocks { get; set; } = new List<BatchStock>();
    public ICollection<BatchLog> BatchLogs { get; set; } = new List<BatchLog>();
}
