using System.Text.Json.Nodes;

namespace decorativeplant_be.Domain.Entities;

public class ProductListing : BaseEntity
{
    public Guid BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    
    public Guid? BatchId { get; set; }
    public PlantBatch? Batch { get; set; }
    
    public JsonNode? ProductInfo { get; set; } // title, slug, price...
    public JsonNode? StatusInfo { get; set; }
    public JsonNode? SeoInfo { get; set; }
    public JsonNode? Images { get; set; }
}
