using System.Text.Json.Nodes;

namespace decorativeplant_be.Domain.Entities;

public class PlantBatch : BaseEntity
{
    public Guid? BranchId { get; set; }
    public Branch? Branch { get; set; }
    
    public Guid? TaxonomyId { get; set; }
    public PlantTaxonomy? Taxonomy { get; set; }
    
    public Guid? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    
    public Guid? ParentBatchId { get; set; }
    public PlantBatch? ParentBatch { get; set; }
    
    public string? BatchCode { get; set; }
    
    public JsonNode? SourceInfo { get; set; } // type, acquisition_date
    public JsonNode? Specs { get; set; } // unit, pot_size, maturity
    
    public int InitialQuantity { get; set; }
    public int CurrentTotalQuantity { get; set; }
}
