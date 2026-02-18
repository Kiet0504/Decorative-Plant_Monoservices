using decorativeplant_be.Application.Features.Inventory.DTOs;
using MediatR;

namespace decorativeplant_be.Application.Features.Inventory.Commands;

public class AdjustStockCommand : IRequest<StockAdjustmentDto>
{
    public Guid BatchId { get; set; }
    public Guid LocationId { get; set; }
    public int QuantityChange { get; set; } // Positive for add, negative for remove
    public string Reason { get; set; } = string.Empty;
    public string Type { get; set; } = "Adjustment"; // Import, Export, Audit, Loss, Adjustment
    public Guid? PerformedBy { get; set; } // Optional, can be set from Claims in Controller
}
