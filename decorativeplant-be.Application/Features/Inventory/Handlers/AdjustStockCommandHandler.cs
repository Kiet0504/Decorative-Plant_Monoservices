using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Inventory.DTOs;
using decorativeplant_be.Application.Features.Inventory.Commands;
using decorativeplant_be.Domain.Entities;
using MediatR;
using System.Text.Json;

namespace decorativeplant_be.Application.Features.Inventory.Handlers;

public class AdjustStockCommandHandler : IRequestHandler<AdjustStockCommand, StockAdjustmentDto>
{
    private readonly IRepositoryFactory _repositoryFactory;
    private readonly IUnitOfWork _unitOfWork;

    public AdjustStockCommandHandler(IRepositoryFactory repositoryFactory, IUnitOfWork unitOfWork)
    {
        _repositoryFactory = repositoryFactory;
        _unitOfWork = unitOfWork;
    }

    public async Task<StockAdjustmentDto> Handle(AdjustStockCommand request, CancellationToken cancellationToken)
    {
        var stockRepo = _repositoryFactory.CreateRepository<BatchStock>();
        
        // Find existing stock record
        var stock = await stockRepo.FirstOrDefaultAsync(
            s => s.BatchId == request.BatchId && s.LocationId == request.LocationId, 
            cancellationToken
        );

        if (stock == null)
        {
            // Create new stock record if it doesn't exist
            stock = new BatchStock
            {
                Id = Guid.NewGuid(),
                BatchId = request.BatchId,
                LocationId = request.LocationId,
                Quantities = JsonSerializer.SerializeToDocument(new BatchStockQuantities { Quantity = 0, ReservedQuantity = 0, AvailableQuantity = 0 }),
                UpdatedAt = DateTime.UtcNow
            };
            await stockRepo.AddAsync(stock, cancellationToken);
        }

        // Parse current quantities
        var quantities = stock.Quantities != null 
            ? JsonSerializer.Deserialize<BatchStockQuantities>(stock.Quantities) 
            : new BatchStockQuantities();
            
        if (quantities == null) quantities = new BatchStockQuantities();

        // Update quantities
        quantities.Quantity += request.QuantityChange;
        quantities.AvailableQuantity = quantities.Quantity - quantities.ReservedQuantity;

        // Validation: Cannot have negative stock
        if (quantities.Quantity < 0)
        {
            throw new InvalidOperationException("Stock quantity cannot be negative.");
        }

        // Save back to JSON
        stock.Quantities = JsonSerializer.SerializeToDocument(quantities);
        stock.UpdatedAt = DateTime.UtcNow;

        // Update Stock entity
        if (stockRepo.ExistsAsync(s => s.Id == stock.Id).Result) 
        {
             // It's already tracked if we found it, or added if new. 
             // EF Core should track changes automatically if fetched from context.
             // But if we used a non-tracking query (check repo impl), we might need Update.
             // Repository implementation uses standard DbSet, so it's tracked.
             await stockRepo.UpdateAsync(stock, cancellationToken);
        }

        // Create Stock Adjustment Log
        var adjustment = new StockAdjustment
        {
            Id = Guid.NewGuid(),
            StockId = stock.Id,
            Type = request.Type,
            QuantityChange = request.QuantityChange,
            Reason = request.Reason,
            CreatedAt = DateTime.UtcNow,
            MetaInfo = JsonSerializer.SerializeToDocument(new 
            { 
                adjusted_by = request.PerformedBy,
                quantities_before_after = new 
                { 
                    before = quantities.Quantity - request.QuantityChange, 
                    after = quantities.Quantity 
                } 
            })
        };

        var adjustmentRepo = _repositoryFactory.CreateRepository<StockAdjustment>();
        await adjustmentRepo.AddAsync(adjustment, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new StockAdjustmentDto
        {
            Id = adjustment.Id,
            BatchId = request.BatchId,
            LocationId = request.LocationId,
            QuantityChange = adjustment.QuantityChange,
            Reason = adjustment.Reason ?? string.Empty,
            Type = adjustment.Type ?? "Adjustment",
            AdjustedAt = adjustment.CreatedAt ?? DateTime.UtcNow
        };
    }

    private class BatchStockQuantities
    {
        public int Quantity { get; set; }
        public int ReservedQuantity { get; set; }
        public int AvailableQuantity { get; set; }
    }
}
