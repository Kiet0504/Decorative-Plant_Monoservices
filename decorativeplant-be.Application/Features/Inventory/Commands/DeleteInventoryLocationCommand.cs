using MediatR;

namespace decorativeplant_be.Application.Features.Inventory.Commands;

public class DeleteInventoryLocationCommand : IRequest<Unit>
{
    public Guid Id { get; set; }

    public DeleteInventoryLocationCommand(Guid id)
    {
        Id = id;
    }
}
