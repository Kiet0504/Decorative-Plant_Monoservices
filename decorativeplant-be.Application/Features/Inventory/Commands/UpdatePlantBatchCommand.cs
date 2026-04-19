using decorativeplant_be.Application.Features.Inventory.DTOs;
using MediatR;
using System.Text.Json.Serialization;

namespace decorativeplant_be.Application.Features.Inventory.Commands;

public class UpdatePlantBatchCommand : IRequest<PlantBatchDto>
{
    public Guid Id { get; set; }
    public string? BatchCode { get; set; }
    public Guid? BranchId { get; set; }
    public Guid? TaxonomyId { get; set; }
    public Guid? SupplierId { get; set; }
    public Guid? ParentBatchId { get; set; }
    public int? InitialQuantity { get; set; }
    public int? CurrentTotalQuantity { get; set; }
    public string? Price { get; set; }
    public Dictionary<string, object>? SourceInfo { get; set; }
    public Dictionary<string, object>? Specs { get; set; }
    public decimal? PurchaseCost { get; set; }
    
    // Detailed stock quantities
    public int? Quantity { get; set; }
    
    [JsonPropertyName("reserved_quantity")]
    public int? ReservedQuantity { get; set; }
    
    [JsonPropertyName("available_quantity")]
    public int? AvailableQuantity { get; set; }
    
    [JsonPropertyName("total_received")]
    public int? TotalReceived { get; set; }
}
