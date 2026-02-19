using decorativeplant_be.Application.Common.DTOs.Common;
using decorativeplant_be.Application.Common.DTOs.Garden;
using decorativeplant_be.Application.Features.Inventory.Commands;
using decorativeplant_be.Application.Features.Inventory.DTOs;
using decorativeplant_be.Application.Features.Inventory.Queries;
using decorativeplant_be.Application.Features.PlantLibrary.DTOs; // For SupplierDto if needed later, but standard DTOs ok
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace decorativeplant_be.API.Controllers;

[ApiController]
[Route("api/inventory")]
[Authorize] // Require staff/admin
public class InventoryController : BaseController
{
    /// <summary>
    /// Create a new inventory location (Warehouse, Zone, Shelf).
    /// </summary>
    [HttpPost("locations")]
    public async Task<ActionResult<ApiResponse<InventoryLocationDto>>> CreateLocation([FromBody] CreateInventoryLocationCommand command)
    {
        var result = await Mediator.Send(command);
        return StatusCode(201, ApiResponse<InventoryLocationDto>.SuccessResponse(result, "Location created.", 201));
    }

    /// <summary>
    /// Adjust stock quantity (Import, Export, Audit, Loss).
    /// </summary>
    [HttpPost("adjustments")]
    public async Task<ActionResult<ApiResponse<StockAdjustmentDto>>> AdjustStock([FromBody] AdjustStockCommand command)
    {
        // TODO: Get User ID from claims and assign to command.PerformedBy
        // For now relying on client or nullable
        var result = await Mediator.Send(command);
        return StatusCode(201, ApiResponse<StockAdjustmentDto>.SuccessResponse(result, "Stock adjusted.", 201));
    }

    /// <summary>
    /// Request a stock transfer.
    /// </summary>
    [HttpPost("transfers/request")]
    public async Task<ActionResult<ApiResponse<StockTransferDto>>> RequestTransfer([FromBody] RequestStockTransferCommand command)
    {
        var result = await Mediator.Send(command);
        return StatusCode(201, ApiResponse<StockTransferDto>.SuccessResponse(result, "Transfer requested.", 201));
    }

    /// <summary>
    /// Approve or Reject a stock transfer.
    /// </summary>
    [HttpPost("transfers/approve")]
    public async Task<ActionResult<ApiResponse<StockTransferDto>>> ApproveTransfer([FromBody] ApproveStockTransferCommand command)
    {
        var result = await Mediator.Send(command);
        return Ok(ApiResponse<StockTransferDto>.SuccessResponse(result, command.Approved ? "Transfer approved." : "Transfer rejected."));
    }

    /// <summary>
    /// Mark transfer as Shipped.
    /// </summary>
    [HttpPost("transfers/ship")]
    public async Task<ActionResult<ApiResponse<StockTransferDto>>> ShipTransfer([FromBody] ShipStockTransferCommand command)
    {
        var result = await Mediator.Send(command);
        return Ok(ApiResponse<StockTransferDto>.SuccessResponse(result, "Transfer shipped."));
    }

    /// <summary>
    /// Mark transfer as Received.
    /// </summary>
    [HttpPost("transfers/receive")]
    public async Task<ActionResult<ApiResponse<StockTransferDto>>> ReceiveTransfer([FromBody] ReceiveStockTransferCommand command)
    {
        var result = await Mediator.Send(command);
        return Ok(ApiResponse<StockTransferDto>.SuccessResponse(result, "Transfer received."));
    }

    /// <summary>
    /// List stock transfers.
    /// </summary>
    [HttpGet("transfers")]
    public async Task<ActionResult<ApiResponse<PagedResultDto<StockTransferDto>>>> ListTransfers([FromQuery] ListStockTransfersQuery query)
    {
        var result = await Mediator.Send(query);
        return Ok(ApiResponse<PagedResultDto<StockTransferDto>>.SuccessResponse(result, "Transfers retrieved."));
    }
}
