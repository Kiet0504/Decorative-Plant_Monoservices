using decorativeplant_be.Application.Features.Inventory.DTOs;
using MediatR;

namespace decorativeplant_be.Application.Features.Inventory.Commands;

public class CreatePlantBatchCommand : IRequest<PlantBatchDto>
{
    public Guid? BranchId { get; set; }
    public Guid? TaxonomyId { get; set; }
    public Guid? SupplierId { get; set; }
    public Guid? ParentBatchId { get; set; }
    
    public Guid? LocationId { get; set; }
    public string? BatchCode { get; set; }
    public string? NewLocationName { get; set; }
    public string? NewLocationType { get; set; }
    public Dictionary<string, object>? NewLocationDetails { get; set; }
    
    public Dictionary<string, object>? SourceInfo { get; set; }
    public Dictionary<string, object>? Specs { get; set; }
    
    public int InitialQuantity { get; set; }
}
