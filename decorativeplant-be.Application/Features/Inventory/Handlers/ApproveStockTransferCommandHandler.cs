using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Inventory.DTOs;
using decorativeplant_be.Application.Features.Inventory.Commands;
using decorativeplant_be.Domain.Entities;
using MediatR;

namespace decorativeplant_be.Application.Features.Inventory.Handlers;

public class ApproveStockTransferCommandHandler : IRequestHandler<ApproveStockTransferCommand, StockTransferDto>
{
    private readonly IRepositoryFactory _repositoryFactory;
    private readonly IUnitOfWork _unitOfWork;

    public ApproveStockTransferCommandHandler(IRepositoryFactory repositoryFactory, IUnitOfWork unitOfWork)
    {
        _repositoryFactory = repositoryFactory;
        _unitOfWork = unitOfWork;
    }

    public async Task<StockTransferDto> Handle(ApproveStockTransferCommand request, CancellationToken cancellationToken)
    {
        var repo = _repositoryFactory.CreateRepository<StockTransfer>();
        var transfer = await repo.GetByIdAsync(request.TransferId, cancellationToken);

        if (transfer == null)
            throw new NotFoundException(nameof(StockTransfer), request.TransferId);

        if (transfer.Status != "Requested")
            throw new ValidationException("Transfer is not in Requested state.");

        transfer.Status = request.Approved ? "Approved" : "Rejected";
        
        if (request.Approved)
        {
            var stockRepo = _repositoryFactory.CreateRepository<BatchStock>();
            var stock = await stockRepo.FirstOrDefaultAsync(s => s.BatchId == transfer.BatchId && s.LocationId == transfer.FromLocationId, cancellationToken);
            
            if (stock != null && stock.Quantities != null)
            {
                var quantities = System.Text.Json.JsonSerializer.Deserialize<BatchStockQuantities>(stock.Quantities);
                if (quantities != null)
                {
                    if (quantities.AvailableQuantity < transfer.Quantity)
                    {
                        throw new ValidationException("Insufficient available stock to approve transfer.");
                    }
                    
                    quantities.AvailableQuantity -= transfer.Quantity;
                    quantities.ReservedQuantity += transfer.Quantity;
                    stock.Quantities = System.Text.Json.JsonSerializer.SerializeToDocument(quantities);
                    await stockRepo.UpdateAsync(stock, cancellationToken);
                }
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return InventoryMapper.ToStockTransferDto(transfer);
    }

    private class BatchStockQuantities
    {
        [System.Text.Json.Serialization.JsonPropertyName("quantity")]
        public int Quantity { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("reserved_quantity")]
        public int ReservedQuantity { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("available_quantity")]
        public int AvailableQuantity { get; set; }
    }
}
