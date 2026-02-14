using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Garden.Commands;
using MediatR;

namespace decorativeplant_be.Application.Features.Garden.Handlers;

public class DeleteGardenPlantCommandHandler : IRequestHandler<DeleteGardenPlantCommand, Unit>
{
    private readonly IGardenRepository _gardenRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteGardenPlantCommandHandler(
        IGardenRepository gardenRepository,
        IUnitOfWork unitOfWork)
    {
        _gardenRepository = gardenRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Unit> Handle(DeleteGardenPlantCommand request, CancellationToken cancellationToken)
    {
        var plant = await _gardenRepository.GetPlantByIdAsync(request.Id, includeTaxonomy: false, cancellationToken);
        if (plant == null || plant.UserId != request.UserId)
        {
            throw new NotFoundException("Garden plant", request.Id);
        }

        if (request.Permanent)
        {
            await _gardenRepository.DeletePlantAsync(plant, cancellationToken);
        }
        else
        {
            plant.IsArchived = true;
            await _gardenRepository.UpdatePlantAsync(plant, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
