using System.Text.Json;

namespace decorativeplant_be.Application.Common.DTOs.ArPreview;

public class ProductModelAssetResponse
{
    public Guid ProductListingId { get; set; }
    public string GlbUrl { get; set; } = string.Empty;
    public decimal DefaultScale { get; set; } = 1m;
    public JsonDocument? BoundingBox { get; set; }
}

