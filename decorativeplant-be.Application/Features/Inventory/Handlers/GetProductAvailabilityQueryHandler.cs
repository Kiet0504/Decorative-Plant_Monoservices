using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Inventory.DTOs;
using decorativeplant_be.Application.Features.Inventory.Queries;
using decorativeplant_be.Domain.Entities;
using MediatR;

namespace decorativeplant_be.Application.Features.Inventory.Handlers;

public class GetProductAvailabilityQueryHandler : IRequestHandler<GetProductAvailabilityQuery, ProductAvailabilityDto>
{
    private readonly IRepositoryFactory _repositoryFactory;

    public GetProductAvailabilityQueryHandler(IRepositoryFactory repositoryFactory)
    {
        _repositoryFactory = repositoryFactory;
    }

    public async Task<ProductAvailabilityDto> Handle(GetProductAvailabilityQuery request, CancellationToken cancellationToken)
    {
        var listingRepo = _repositoryFactory.CreateRepository<ProductListing>();
        var listing = await listingRepo.GetByIdAsync(request.ProductListingId, cancellationToken);

        if (listing == null)
        {
            throw new NotFoundException(nameof(ProductListing), request.ProductListingId);
        }

        int quantity = 0;
        string status = "OutOfStock";

        if (listing.BatchId.HasValue)
        {
            var batchRepo = _repositoryFactory.CreateRepository<PlantBatch>();
            var batch = await batchRepo.GetByIdAsync(listing.BatchId.Value, cancellationToken);
            if (batch != null)
            {
                quantity = batch.CurrentTotalQuantity ?? 0;
            }
        }

        if (quantity > 0) status = "InStock";

        return new ProductAvailabilityDto
        {
            ProductListingId = listing.Id,
            BatchId = listing.BatchId,
            TotalQuantity = quantity,
            Status = status
        };
    }
}
