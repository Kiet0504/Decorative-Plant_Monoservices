using decorativeplant_be.Application.Features.Inventory.DTOs;
using MediatR;

namespace decorativeplant_be.Application.Features.Inventory.Commands;

public class UpdateInventoryLocationCommand : IRequest<InventoryLocationDto>
{
    public Guid Id { get; set; }
    public Guid? ParentLocationId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "Shelf";
    public string? Description { get; set; }
    public int? Capacity { get; set; }
}
