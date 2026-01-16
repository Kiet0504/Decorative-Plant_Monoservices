namespace decorativeplant_be.Domain.Entities;

public class PlantBatch : BaseEntity
{
    public Guid StoreId { get; set; }
    public Store Store { get; set; } = null!;
    
    public Guid TaxonomyId { get; set; }
    public PlantTaxonomy Taxonomy { get; set; } = null!;
    
    public Guid? ParentBatchId { get; set; }
    public PlantBatch? ParentBatch { get; set; }
    
    public string BatchCode { get; set; } = string.Empty;
    public DateTime? SowingDate { get; set; }
    public string SourceType { get; set; } = string.Empty;
}
