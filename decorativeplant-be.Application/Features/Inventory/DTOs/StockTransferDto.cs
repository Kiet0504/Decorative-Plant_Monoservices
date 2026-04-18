namespace decorativeplant_be.Application.Features.Inventory.DTOs;

public class StockTransferDto
{
    public Guid Id { get; set; }
    public string? TransferCode { get; set; }
    public Guid BatchId { get; set; }
    public Guid FromBranchId { get; set; }
    public Guid ToBranchId { get; set; }
    public Guid FromLocationId { get; set; }
    public Guid ToLocationId { get; set; }
    public string? FromBranchName { get; set; }
    public string? FromBranchAddress { get; set; }
    public string? ToBranchName { get; set; }
    public string? ToBranchAddress { get; set; }
    public string? SpeciesName { get; set; }
    public int Quantity { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ShippedAt { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public int? FromStockSnapshot { get; set; }
    public int? ToStockSnapshot { get; set; }
}
