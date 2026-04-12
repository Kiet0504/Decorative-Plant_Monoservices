using MediatR;

namespace decorativeplant_be.Application.Features.Inventory.Commands;

public class DeletePlantBatchCommand : IRequest<Unit>
{
    public Guid Id { get; set; }

    public DeletePlantBatchCommand(Guid id)
    {
        Id = id;
    }
}
