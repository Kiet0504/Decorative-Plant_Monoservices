using MediatR;

namespace decorativeplant_be.Application.Features.Inventory.Commands;

public class PublishBatchToStockCommand : IRequest<bool>
{
    public Guid BatchId { get; set; }
    public int Quantity { get; set; }
    public Guid? SourceLocationId { get; set; }
    public string? Price { get; set; }
}
