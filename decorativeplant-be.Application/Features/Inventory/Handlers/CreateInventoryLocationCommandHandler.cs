using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Inventory.DTOs;
using decorativeplant_be.Domain.Entities;
using MediatR;
using System.Text.Json;
using decorativeplant_be.Application.Features.Inventory.Commands;

namespace decorativeplant_be.Application.Features.Inventory.Handlers;

public class CreateInventoryLocationCommandHandler : IRequestHandler<CreateInventoryLocationCommand, InventoryLocationDto>
{
    private readonly IRepositoryFactory _repositoryFactory;
    private readonly IUnitOfWork _unitOfWork;

    public CreateInventoryLocationCommandHandler(IRepositoryFactory repositoryFactory, IUnitOfWork unitOfWork)
    {
        _repositoryFactory = repositoryFactory;
        _unitOfWork = unitOfWork;
    }

    public async Task<InventoryLocationDto> Handle(CreateInventoryLocationCommand request, CancellationToken cancellationToken)
    {
        var details = new
        {
            description = request.Description,
            capacity = request.Capacity,
            environment_type = request.EnvironmentType?.Trim(),
            target_growth_stage = request.TargetGrowthStage,
            position_x = request.PositionX ?? 0,
            position_y = request.PositionY ?? 0
        };

        var entity = new InventoryLocation
        {
            Id = Guid.NewGuid(),
            BranchId = request.BranchId,
            ParentLocationId = request.ParentLocationId,
            TaxonomyId = request.TaxonomyId,
            Code = request.Code,
            Name = request.Name,
            Type = request.Type,
            Details = JsonSerializer.SerializeToDocument(details)
        };

        var repo = _repositoryFactory.CreateRepository<InventoryLocation>();
        await repo.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new InventoryLocationDto
        {
            Id = entity.Id,
            BranchId = entity.BranchId,
            ParentLocationId = entity.ParentLocationId,
            TaxonomyId = entity.TaxonomyId,
            Code = entity.Code,
            Name = entity.Name,
            Type = entity.Type,
            Description = request.Description,
            Capacity = request.Capacity,
            EnvironmentType = request.EnvironmentType,
            TargetGrowthStage = request.TargetGrowthStage,
            PositionX = request.PositionX,
            PositionY = request.PositionY
        };
    }
}
