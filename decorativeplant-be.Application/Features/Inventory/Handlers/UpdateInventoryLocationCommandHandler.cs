using System.Text.Json;
using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Inventory.Commands;
using decorativeplant_be.Application.Features.Inventory.DTOs;
using decorativeplant_be.Domain.Entities;
using MediatR;

namespace decorativeplant_be.Application.Features.Inventory.Handlers;

public class UpdateInventoryLocationCommandHandler : IRequestHandler<UpdateInventoryLocationCommand, InventoryLocationDto>
{
    private readonly IRepositoryFactory _repositoryFactory;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateInventoryLocationCommandHandler(IRepositoryFactory repositoryFactory, IUnitOfWork unitOfWork)
    {
        _repositoryFactory = repositoryFactory;
        _unitOfWork = unitOfWork;
    }

    public async Task<InventoryLocationDto> Handle(UpdateInventoryLocationCommand request, CancellationToken cancellationToken)
    {
        var repo = _repositoryFactory.CreateRepository<InventoryLocation>();
        var location = await repo.GetByIdAsync(request.Id);

        if (location == null)
        {
            throw new NotFoundException(nameof(InventoryLocation), request.Id);
        }

        location.ParentLocationId = request.ParentLocationId;
        location.Code = request.Code;
        location.Name = request.Name;
        location.Type = request.Type;

        var detailsObj = new
        {
            description = request.Description,
            capacity = request.Capacity
        };
        location.Details = JsonSerializer.SerializeToDocument(detailsObj);

        await repo.UpdateAsync(location, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new InventoryLocationDto
        {
            Id = location.Id,
            BranchId = location.BranchId,
            ParentLocationId = location.ParentLocationId,
            Code = location.Code,
            Name = location.Name,
            Type = location.Type,
            Description = request.Description,
            Capacity = request.Capacity
        };
    }
}
