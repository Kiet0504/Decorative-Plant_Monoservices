using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.PlantLibrary.DTOs;
using decorativeplant_be.Domain.Entities;
using MediatR;
using System.Text.Json;
using decorativeplant_be.Application.Features.PlantLibrary.Commands;

namespace decorativeplant_be.Application.Features.PlantLibrary.Handlers;

public class CreateSupplierCommandHandler : IRequestHandler<CreateSupplierCommand, SupplierDto>
{
    private readonly IRepositoryFactory _repositoryFactory;
    private readonly IUnitOfWork _unitOfWork;

    public CreateSupplierCommandHandler(IRepositoryFactory repositoryFactory, IUnitOfWork unitOfWork)
    {
        _repositoryFactory = repositoryFactory;
        _unitOfWork = unitOfWork;
    }

    public async Task<SupplierDto> Handle(CreateSupplierCommand request, CancellationToken cancellationToken)
    {
        var entity = new Supplier
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            ContactInfo = SupplierMapper.BuildContactInfo(request.ContactInfo, request.Address),
            // Address removed from entity
        };

        var repo = _repositoryFactory.CreateRepository<Supplier>();
        await repo.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return SupplierMapper.ToDto(entity);
    }
}
