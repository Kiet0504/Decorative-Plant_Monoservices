using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Inventory.Commands;
using decorativeplant_be.Domain.Entities;
using MediatR;

namespace decorativeplant_be.Application.Features.Inventory.Handlers;

public class DeletePlantBatchCommandHandler : IRequestHandler<DeletePlantBatchCommand, Unit>
{
    private readonly IRepositoryFactory _repositoryFactory;
    private readonly IUnitOfWork _unitOfWork;

    public DeletePlantBatchCommandHandler(IRepositoryFactory repositoryFactory, IUnitOfWork unitOfWork)
    {
        _repositoryFactory = repositoryFactory;
        _unitOfWork = unitOfWork;
    }

    public async Task<Unit> Handle(DeletePlantBatchCommand request, CancellationToken cancellationToken)
    {
        var batchRepo = _repositoryFactory.CreateRepository<PlantBatch>();
        var batch = await batchRepo.GetByIdAsync(request.Id, cancellationToken);

        if (batch == null)
        {
            throw new NotFoundException("Plant batch", request.Id);
        }

        // 1. SAFETY CHECKS (Blockers that cannot be cascaded)
        
        // Block if batch has sales history (OrderItems) - Preserve for accountancy
        var orderRepo = _repositoryFactory.CreateRepository<OrderItem>();
        var hasSales = await orderRepo.ExistsAsync(x => x.BatchId == request.Id, cancellationToken);
        if (hasSales)
        {
            throw new BadRequestException("Cannot delete batch because it has sales history (Orders). Consider marking it as inactive instead.");
        }

        // Block if it has child batches (Hierarchy)
        var hasChildren = await batchRepo.ExistsAsync(x => x.ParentBatchId == request.Id, cancellationToken);
        if (hasChildren)
        {
            throw new BadRequestException("Cannot delete batch because it has child batches (Propagation). Please delete child batches first.");
        }

        // 2. CASCADE DELETE (Operational Data)

        // Delete Cultivation Logs
        var logRepo = _repositoryFactory.CreateRepository<CultivationLog>();
        var logs = await logRepo.FindAsync(x => x.BatchId == request.Id, cancellationToken);
        foreach (var log in logs) await logRepo.DeleteAsync(log, cancellationToken);

        // Delete Health Incidents
        var incidentRepo = _repositoryFactory.CreateRepository<HealthIncident>();
        var incidents = await incidentRepo.FindAsync(x => x.BatchId == request.Id, cancellationToken);
        foreach (var incident in incidents) await incidentRepo.DeleteAsync(incident, cancellationToken);

        // Delete Stocks and Adjustments
        var stockRepo = _repositoryFactory.CreateRepository<BatchStock>();
        var adjustmentRepo = _repositoryFactory.CreateRepository<StockAdjustment>();
        var stocks = await stockRepo.FindAsync(x => x.BatchId == request.Id, cancellationToken);
        foreach (var stock in stocks)
        {
            var adjs = await adjustmentRepo.FindAsync(a => a.StockId == stock.Id, cancellationToken);
            foreach (var adj in adjs) await adjustmentRepo.DeleteAsync(adj, cancellationToken);
            await stockRepo.DeleteAsync(stock, cancellationToken);
        }

        // Delete Stock Transfers
        var transferRepo = _repositoryFactory.CreateRepository<StockTransfer>();
        var transfers = await transferRepo.FindAsync(x => x.BatchId == request.Id, cancellationToken);
        foreach (var transfer in transfers) await transferRepo.DeleteAsync(transfer, cancellationToken);

        // Delete Product Listings
        var listingRepo = _repositoryFactory.CreateRepository<ProductListing>();
        var listings = await listingRepo.FindAsync(x => x.BatchId == request.Id, cancellationToken);
        foreach (var listing in listings) await listingRepo.DeleteAsync(listing, cancellationToken);

        // 3. FINAL DELETE
        await batchRepo.DeleteAsync(batch, cancellationToken);
        
        // COMMIT CHANGES
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        
        return Unit.Value;
    }
}
