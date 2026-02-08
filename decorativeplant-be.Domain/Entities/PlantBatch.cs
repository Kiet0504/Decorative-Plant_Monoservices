using System.Text.Json;

namespace decorativeplant_be.Domain.Entities;

/// <summary>
/// Batch of plants (same source/specs). JSONB: source_info, specs. See JSONB_SCHEMA_REFERENCE.md
/// </summary>
public class PlantBatch
{
    public Guid Id { get; set; }
    public Guid? BranchId { get; set; }
    public Guid? TaxonomyId { get; set; }
    public Guid? SupplierId { get; set; }
    public Guid? ParentBatchId { get; set; }
    public string? BatchCode { get; set; }
    public JsonDocument? SourceInfo { get; set; }
    public JsonDocument? Specs { get; set; }
    public int? InitialQuantity { get; set; }
    public int? CurrentTotalQuantity { get; set; }
    public DateTime? CreatedAt { get; set; }

    public Branch? Branch { get; set; }
    public PlantTaxonomy? Taxonomy { get; set; }
    public Supplier? Supplier { get; set; }
    public PlantBatch? ParentBatch { get; set; }
    public ICollection<PlantBatch> ChildBatches { get; set; } = new List<PlantBatch>();
    public ICollection<BatchStock> BatchStocks { get; set; } = new List<BatchStock>();
    public ICollection<CultivationLog> CultivationLogs { get; set; } = new List<CultivationLog>();
    public ICollection<HealthIncident> HealthIncidents { get; set; } = new List<HealthIncident>();
    public ICollection<StockTransfer> StockTransfers { get; set; } = new List<StockTransfer>();
    public ICollection<ProductListing> ProductListings { get; set; } = new List<ProductListing>();
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}
