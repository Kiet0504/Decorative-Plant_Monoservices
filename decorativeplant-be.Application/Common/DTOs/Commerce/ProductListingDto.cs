namespace decorativeplant_be.Application.Common.DTOs.Commerce;

// ── Request DTOs ──
public class CreateProductListingRequest
{
    public Guid BranchId { get; set; }
    public Guid? BatchId { get; set; }
    // ProductInfo JSONB
    public string Title { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public string? Description { get; set; }
    public string Price { get; set; } = "0";
    public int MinOrder { get; set; } = 1;
    public int MaxOrder { get; set; } = 10;
    // StatusInfo JSONB
    public string Status { get; set; } = "draft";
    public string Visibility { get; set; } = "public";
    public bool Featured { get; set; }
    public List<string> Tags { get; set; } = new();
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
    public string? Slug { get; set; }
    public string? Description { get; set; }
    public string? Price { get; set; }
    public int? MinOrder { get; set; }
    public int? MaxOrder { get; set; }
    public string? Status { get; set; }
    public string? Visibility { get; set; }
    public bool? Featured { get; set; }
    public List<string>? Tags { get; set; }
    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }
    public string? MetaKeywords { get; set; }
    public List<ProductImageDto>? Images { get; set; }
}

// ── Response DTOs ──
public class ProductListingResponse
{
    public Guid Id { get; set; }
    public Guid? BranchId { get; set; }
    public Guid? BatchId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public string? Description { get; set; }
    public string Price { get; set; } = "0";
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
    public string? Slug { get; set; }
    public string? Description { get; set; }
    public string? Price { get; set; }
    public int MinOrder { get; set; } = 1;
    public int MaxOrder { get; set; } = 10;
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
