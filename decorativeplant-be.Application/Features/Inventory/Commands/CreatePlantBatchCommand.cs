using decorativeplant_be.Application.Features.Inventory.DTOs;
using MediatR;

namespace decorativeplant_be.Application.Features.Inventory.Commands;

public class CreatePlantBatchCommand : IRequest<PlantBatchDto>
{
    public Guid? BranchId { get; set; }
    public Guid? TaxonomyId { get; set; }
    public Guid? SupplierId { get; set; }
    public Guid? ParentBatchId { get; set; }
    
    public Dictionary<string, object>? SourceInfo { get; set; }
    public Dictionary<string, object>? Specs { get; set; }
    
    public int InitialQuantity { get; set; }
}
