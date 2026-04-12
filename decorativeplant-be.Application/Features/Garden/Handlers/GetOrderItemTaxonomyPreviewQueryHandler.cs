using decorativeplant_be.Application.Common.DTOs.Garden;
using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Garden;
using decorativeplant_be.Application.Features.Garden.Queries;
using decorativeplant_be.Domain.Entities;
using MediatR;

namespace decorativeplant_be.Application.Features.Garden.Handlers;

public sealed class GetOrderItemTaxonomyPreviewQueryHandler
    : IRequestHandler<GetOrderItemTaxonomyPreviewQuery, OrderItemTaxonomyPreviewDto>
{
    private readonly IRepositoryFactory _repositoryFactory;

    public GetOrderItemTaxonomyPreviewQueryHandler(IRepositoryFactory repositoryFactory)
    {
        _repositoryFactory = repositoryFactory;
    }

    public async Task<OrderItemTaxonomyPreviewDto> Handle(
        GetOrderItemTaxonomyPreviewQuery request,
        CancellationToken cancellationToken)
    {
        var orderItemRepo = _repositoryFactory.CreateRepository<OrderItem>();
        var orderRepo = _repositoryFactory.CreateRepository<OrderHeader>();
        var listingRepo = _repositoryFactory.CreateRepository<ProductListing>();
        var batchRepo = _repositoryFactory.CreateRepository<PlantBatch>();
        var taxonomyRepo = _repositoryFactory.CreateRepository<PlantTaxonomy>();

        var orderItem = await orderItemRepo.GetByIdAsync(request.OrderItemId, cancellationToken);
        if (orderItem == null)
        {
            throw new NotFoundException($"Order item not found: {request.OrderItemId}");
        }

        if (!orderItem.OrderId.HasValue)
        {
            throw new BadRequestException($"Order item has no orderId: {request.OrderItemId}");
        }

        var order = await orderRepo.GetByIdAsync(orderItem.OrderId.Value, cancellationToken);
        if (order == null)
        {
            throw new NotFoundException($"Order not found: {orderItem.OrderId.Value}");
        }

        if (order.UserId != request.UserId)
        {
            throw new UnauthorizedException("Access denied: order item does not belong to current user.");
        }

        var batchId = orderItem.BatchId;
        if (!batchId.HasValue && orderItem.ListingId.HasValue)
        {
            var listing = await listingRepo.GetByIdAsync(orderItem.ListingId.Value, cancellationToken);
            batchId = listing?.BatchId;
        }

        Guid? taxonomyId = null;
        if (batchId.HasValue)
        {
            var batch = await batchRepo.GetByIdAsync(batchId.Value, cancellationToken);
            taxonomyId = batch?.TaxonomyId;
        }

        if (!taxonomyId.HasValue)
        {
            return new OrderItemTaxonomyPreviewDto { Resolved = false };
        }

        var exists = await taxonomyRepo.ExistsAsync(t => t.Id == taxonomyId.Value, cancellationToken);
        if (!exists)
        {
            return new OrderItemTaxonomyPreviewDto { Resolved = false };
        }

        var taxonomy = await taxonomyRepo.GetByIdAsync(taxonomyId.Value, cancellationToken);
        if (taxonomy == null)
        {
            return new OrderItemTaxonomyPreviewDto { Resolved = false };
        }

        var summary = GardenPlantMapper.ToTaxonomySummaryDto(taxonomy);
        return new OrderItemTaxonomyPreviewDto
        {
            Resolved = true,
            TaxonomyId = summary.Id,
            ScientificName = summary.ScientificName,
            CommonName = summary.CommonName
        };
    }
}
