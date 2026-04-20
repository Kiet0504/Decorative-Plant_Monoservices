using System.Text.Json;

namespace decorativeplant_be.Application.Features.Inventory.DTOs;

public class InventoryLocationDto
{
    public Guid Id { get; set; }
    public Guid? BranchId { get; set; }
    public Guid? ParentLocationId { get; set; }
    public string? Code { get; set; }
    public string? Name { get; set; }
    public string? Type { get; set; } // e.g. Warehouse, Zone, Shelf
    public string? Description { get; set; }
    public int? Capacity { get; set; }
    public string? EnvironmentType { get; set; }
    public double? PositionX { get; set; }
    public double? PositionY { get; set; }
    public List<HostedBatchPreviewDto> HostedBatches { get; set; } = new();
}

public class HostedBatchPreviewDto
{
    public Guid Id { get; set; }
    public string? BatchCode { get; set; }
    public string? SpeciesName { get; set; }
    public string? ImageUrl { get; set; }
}
