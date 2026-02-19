using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.PlantLibrary.Commands;
using decorativeplant_be.Domain.Entities;
using MediatR;

namespace decorativeplant_be.Application.Features.PlantLibrary.Handlers;

public class DeletePlantTaxonomyCommandHandler : IRequestHandler<DeletePlantTaxonomyCommand, Unit>
{
    private readonly IRepositoryFactory _repositoryFactory;
    private readonly IUnitOfWork _unitOfWork;

    public DeletePlantTaxonomyCommandHandler(IRepositoryFactory repositoryFactory, IUnitOfWork unitOfWork)
    {
        _repositoryFactory = repositoryFactory;
        _unitOfWork = unitOfWork;
    }

    public async Task<Unit> Handle(DeletePlantTaxonomyCommand request, CancellationToken cancellationToken)
    {
        var repo = _repositoryFactory.CreateRepository<PlantTaxonomy>();
        var entity = await repo.GetByIdAsync(request.Id, cancellationToken);

        if (entity == null)
        {
            throw new NotFoundException(nameof(PlantTaxonomy), request.Id);
        }

        // Ideally check for usage before delete (e.g. if associated with GardenPlants or Batches)
        // For now, assuming direct delete is allowed or DB FK constraints will block it.
        // Actually, let's just delete.

        await repo.DeleteAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
