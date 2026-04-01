using System.Text.Json.Serialization;

namespace decorativeplant_be.Application.Features.Inventory.DTOs;

public class LowStockItemDto
{
    [JsonPropertyName("productId")]
    public string ProductId { get; set; } = string.Empty;

    [JsonPropertyName("productName")]
    public string ProductName { get; set; } = string.Empty;

    [JsonPropertyName("price")]
    public string Price { get; set; } = "0";

    [JsonPropertyName("category")]
    public string Category { get; set; } = "Uncategorized";

    [JsonPropertyName("currentStock")]
    public int CurrentStock { get; set; }

    [JsonPropertyName("threshold")]
    public int Threshold { get; set; }

    [JsonPropertyName("branchName")]
    public string BranchName { get; set; } = string.Empty;

    [JsonPropertyName("batchId")]
    public Guid BatchId { get; set; }

    [JsonPropertyName("batchCode")]
    public string BatchCode { get; set; } = string.Empty;
}

public class ProductAvailabilityDto
{
    public Guid ProductListingId { get; set; }
    public Guid? BatchId { get; set; }
    public int TotalQuantity { get; set; }
    public string Status { get; set; } = "Unknown";
}
