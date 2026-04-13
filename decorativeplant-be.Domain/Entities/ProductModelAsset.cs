using System.Text.Json;

namespace decorativeplant_be.Domain.Entities;

public class ProductModelAsset
{
    public Guid Id { get; set; }

    public Guid ProductListingId { get; set; }
    public ProductListing? ProductListing { get; set; }

    public string GlbUrl { get; set; } = string.Empty;

    /// <summary>
    /// Default scale multiplier applied in viewer.
    /// </summary>
    public decimal DefaultScale { get; set; } = 1m;

    /// <summary>
    /// Bounding box in model local space. JSONB shape:
    /// { "min":[x,y,z], "max":[x,y,z] } (optionally center/size).
    /// Used to correct bad pivots by recentring/grounding.
    /// </summary>
    public JsonDocument? BoundingBox { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

