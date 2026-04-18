using System.Text.Json;

namespace decorativeplant_be.Application.Common.DTOs.Commerce;

// ── Request DTOs ──
public class CreateProductListingRequest
{
    public Guid? BranchId { get; set; }
    public Guid? BatchId { get; set; }
    // ProductInfo JSONB
    public string Title { get; set; } = string.Empty;
    public string? ScientificName { get; set; }
    public string? Slug { get; set; }
    public string? Description { get; set; }
    public string Price { get; set; } = "0";
    public int StockQuantity { get; set; }
    public int MinOrder { get; set; } = 1;
    public int MaxOrder { get; set; } = 10;
    // StatusInfo JSONB
    public string Status { get; set; } = "draft";
    public string Visibility { get; set; } = "public";
    public bool Featured { get; set; }
    public List<string> Tags { get; set; } = new();
    // Botanical Overlays
    public object? CareInfo { get; set; }
    public object? GrowthInfo { get; set; }
    public object? TaxonomyInfo { get; set; }
    // SeoInfo JSONB
    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }
    public string? MetaKeywords { get; set; }
    // Images JSONB
    public List<ProductImageDto> Images { get; set; } = new();
}

public class UpdateProductListingRequest
{
    public string? Title { get; set; }
    public string? ScientificName { get; set; }
    public string? Slug { get; set; }
    public string? Description { get; set; }
    public string? Price { get; set; }
    public int? StockQuantity { get; set; }
    public int? MinOrder { get; set; }
    public int? MaxOrder { get; set; }
    public string? Status { get; set; }
    public string? Visibility { get; set; }
    public bool? Featured { get; set; }
    public List<string>? Tags { get; set; }
    // Botanical Overlays
    public object? CareInfo { get; set; }
    public object? GrowthInfo { get; set; }
    public object? TaxonomyInfo { get; set; }
    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }
    public string? MetaKeywords { get; set; }
    public List<ProductImageDto>? Images { get; set; }
    public bool? SyncToAllBranches { get; set; }
}

// ── Response DTOs ──
public class ProductListingResponse
{
    public Guid Id { get; set; }
    public Guid? BranchId { get; set; }
    public string? BranchName { get; set; }
    public string? BranchAddress { get; set; }
    public Guid? BatchId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? ScientificName { get; set; }
    public string? Slug { get; set; }
    public string? Description { get; set; }
    public string Price { get; set; } = "0";
    public string MaxPrice { get; set; } = "0";
    public int StockQuantity { get; set; }
    public int MinOrder { get; set; }
    public int MaxOrder { get; set; }
    public string Status { get; set; } = "draft";
    public string Visibility { get; set; } = "public";
    public bool Featured { get; set; }
    public int ViewCount { get; set; }
    public int SoldCount { get; set; }
    public List<string> Tags { get; set; } = new();
    public List<ProductImageDto> Images { get; set; } = new();
    public DateTime? CreatedAt { get; set; }

    // Botanical Details
    public JsonDocument? CareInfo { get; set; }
    public JsonDocument? GrowthInfo { get; set; }
    public JsonDocument? TaxonomyInfo { get; set; }

    // Unified Product fields (Chain Store model)
    public int BatchTotalQuantity { get; set; }
    public int BatchReservedQuantity { get; set; }
    public int BatchTotalReceived { get; set; }
    public int TotalSystemStock { get; set; }
    public Guid? StockLocationId { get; set; }
    public Guid? TaxonomyId { get; set; }
    public int AvailableBranches { get; set; }
    public bool HasPriceRange { get; set; }
    public List<BranchStockDto> BranchStocks { get; set; } = new();

    // Batch & Logistics details
    public string? BatchCode { get; set; }
    public DateTime? BatchCreatedAt { get; set; }
    public string? ImportedByName { get; set; }
}

public class BranchStockDto
{
    public Guid ListingId { get; set; }
    public Guid BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public string BranchAddress { get; set; } = string.Empty;
    public JsonDocument? OperatingHours { get; set; }
    public string Price { get; set; } = "0";
    public int StockQuantity { get; set; }
}

public class ProductImageDto
{
    public string Url { get; set; } = string.Empty;
    public string? Alt { get; set; }
    public bool IsPrimary { get; set; }
    public int SortOrder { get; set; }
}

// ── JSONB helper models ──
public class ProductInfoJsonb
{
    public string? Title { get; set; }
    public string? ScientificName { get; set; }
    public string? Slug { get; set; }
    public string? Description { get; set; }
    public string? Price { get; set; }
    public int MinOrder { get; set; } = 1;
    public int MaxOrder { get; set; } = 10;
    // Botanical Info (Overlays from Taxonomy)
    public JsonDocument? CareInfo { get; set; }
    public JsonDocument? GrowthInfo { get; set; }
    public JsonDocument? TaxonomyInfo { get; set; }
}

public class StatusInfoJsonb
{
    public string? Status { get; set; }
    public string? Visibility { get; set; }
    public bool Featured { get; set; }
    public int ViewCount { get; set; }
    public int SoldCount { get; set; }
    public List<string> Tags { get; set; } = new();
}

public class SeoInfoJsonb
{
    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }
    public string? MetaKeywords { get; set; }
}
