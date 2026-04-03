using System.Text.Json;

namespace decorativeplant_be.Domain.Entities;

/// <summary>
/// Order line item. JSONB: pricing, snapshots. See JSONB_SCHEMA_REFERENCE.md
/// </summary>
public class OrderItem
{
    public Guid Id { get; set; }
    public Guid? OrderId { get; set; }
    public Guid? ListingId { get; set; }
    public Guid? StockId { get; set; }
    public Guid? BatchId { get; set; }
    public Guid? BranchId { get; set; }
    public int Quantity { get; set; }
    public JsonDocument? Pricing { get; set; }
    public JsonDocument? Snapshots { get; set; }

    public OrderHeader? Order { get; set; }
    public ProductListing? Listing { get; set; }
    public BatchStock? Stock { get; set; }
    public PlantBatch? Batch { get; set; }
    public Branch? Branch { get; set; }
}
