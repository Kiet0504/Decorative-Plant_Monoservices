using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Inventory.Commands;
using decorativeplant_be.Domain.Entities;
using MediatR;

namespace decorativeplant_be.Application.Features.Inventory.Handlers;

public class DeleteInventoryLocationCommandHandler : IRequestHandler<DeleteInventoryLocationCommand, Unit>
{
    private readonly IRepositoryFactory _repositoryFactory;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteInventoryLocationCommandHandler(IRepositoryFactory repositoryFactory, IUnitOfWork unitOfWork)
    {
        _repositoryFactory = repositoryFactory;
        _unitOfWork = unitOfWork;
    }

    public async Task<Unit> Handle(DeleteInventoryLocationCommand request, CancellationToken cancellationToken)
    {
        var repo = _repositoryFactory.CreateRepository<InventoryLocation>();
        var location = await repo.GetByIdAsync(request.Id);

        if (location == null)
        {
            throw new NotFoundException(nameof(InventoryLocation), request.Id);
        }

        await repo.DeleteAsync(location, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
