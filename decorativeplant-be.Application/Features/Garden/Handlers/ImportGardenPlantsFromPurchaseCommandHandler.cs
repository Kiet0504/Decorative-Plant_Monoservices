using decorativeplant_be.Application.Common.DTOs.Garden;
using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Garden.Commands;
using decorativeplant_be.Domain.Entities;
using MediatR;

namespace decorativeplant_be.Application.Features.Garden.Handlers;

public class ImportGardenPlantsFromPurchaseCommandHandler : IRequestHandler<ImportGardenPlantsFromPurchaseCommand, IReadOnlyList<GardenPlantDto>>
{
    private const int MaxPlantsCreatedPerOrderItem = 20;

    private readonly IGardenRepository _gardenRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRepositoryFactory _repositoryFactory;

    public ImportGardenPlantsFromPurchaseCommandHandler(
        IGardenRepository gardenRepository,
        IUnitOfWork unitOfWork,
        IRepositoryFactory repositoryFactory)
    {
        _gardenRepository = gardenRepository;
        _unitOfWork = unitOfWork;
        _repositoryFactory = repositoryFactory;
    }

    public async Task<IReadOnlyList<GardenPlantDto>> Handle(ImportGardenPlantsFromPurchaseCommand request, CancellationToken cancellationToken)
    {
        var orderItemRepo = _repositoryFactory.CreateRepository<OrderItem>();
        var orderRepo = _repositoryFactory.CreateRepository<OrderHeader>();
        var listingRepo = _repositoryFactory.CreateRepository<ProductListing>();
        var batchRepo = _repositoryFactory.CreateRepository<PlantBatch>();
        var taxonomyRepo = _repositoryFactory.CreateRepository<PlantTaxonomy>();

        var createdPlantIds = new List<Guid>();

        foreach (var orderItemId in request.OrderItemIds)
        {
            var orderItem = await orderItemRepo.GetByIdAsync(orderItemId, cancellationToken);
            if (orderItem == null)
            {
                throw new NotFoundException($"Order item not found: {orderItemId}");
            }

            if (!orderItem.OrderId.HasValue)
            {
                throw new BadRequestException($"Order item has no orderId: {orderItemId}");
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

            if (taxonomyId.HasValue)
            {
                var exists = await taxonomyRepo.ExistsAsync(t => t.Id == taxonomyId.Value, cancellationToken);
                if (!exists)
                {
                    taxonomyId = null;
                }
            }

            var adoptedDate = request.AdoptedDate;
            if (string.IsNullOrWhiteSpace(adoptedDate) && order.CreatedAt.HasValue)
            {
                adoptedDate = order.CreatedAt.Value.ToUniversalTime().ToString("O");
            }

            var purchaseInfo = new Dictionary<string, object?>
            {
                ["order_id"] = order.Id.ToString(),
                ["order_item_id"] = orderItem.Id.ToString(),
                ["listing_id"] = orderItem.ListingId?.ToString(),
                ["batch_id"] = batchId?.ToString(),
                ["quantity"] = orderItem.Quantity
            };

            string? shopProductTitle = null;
            if (orderItem.Snapshots != null)
            {
                var sn = orderItem.Snapshots.RootElement;
                if (sn.TryGetProperty("title_snapshot", out var ts))
                {
                    shopProductTitle = ts.GetString();
                }
            }

            var extras = new Dictionary<string, object?> { ["purchase"] = purchaseInfo };
            if (!string.IsNullOrWhiteSpace(shopProductTitle))
            {
                extras["shop_product_title"] = shopProductTitle.Trim();
            }

            var createCount = request.CreateMode == PurchaseImportCreateMode.OnePerQuantity
                ? Math.Min(Math.Max(orderItem.Quantity, 1), MaxPlantsCreatedPerOrderItem)
                : 1;

            for (var i = 0; i < createCount; i++)
            {
                var details = GardenPlantMapper.BuildDetailsJson(
                    request.Nickname,
                    request.Location,
                    source: "purchased",
                    adoptedDate: adoptedDate,
                    health: request.Health,
                    size: request.Size,
                    milestones: null,
                    extras: extras);

                var plant = new GardenPlant
                {
                    Id = Guid.NewGuid(),
                    UserId = request.UserId,
                    TaxonomyId = taxonomyId,
                    Details = details,
                    ImageUrl = request.ImageUrl,
                    IsArchived = false,
                    CreatedAt = DateTime.UtcNow
                };

                await _gardenRepository.AddPlantAsync(plant, cancellationToken);
                createdPlantIds.Add(plant.Id);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var result = new List<GardenPlantDto>();
        foreach (var id in createdPlantIds)
        {
            var created = await _gardenRepository.GetPlantByIdAsync(id, includeTaxonomy: true, cancellationToken);
            if (created != null)
            {
                result.Add(GardenPlantMapper.ToDto(created));
            }
        }

        return result;
    }
}

