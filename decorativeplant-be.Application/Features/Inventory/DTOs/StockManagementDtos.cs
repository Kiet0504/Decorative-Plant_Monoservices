namespace decorativeplant_be.Application.Features.Inventory.DTOs;

public class LowStockItemDto
{
    public Guid BatchId { get; set; }
    public string BatchCode { get; set; } = string.Empty;
    public string? SpeciesName { get; set; }
    public Guid? BranchId { get; set; }
    public string? BranchName { get; set; }
    public int CurrentQuantity { get; set; }
}

public class ProductAvailabilityDto
{
    public Guid ProductListingId { get; set; }
    public Guid? BatchId { get; set; }
    public int TotalQuantity { get; set; }
    public string Status { get; set; } = "Unknown";
}
