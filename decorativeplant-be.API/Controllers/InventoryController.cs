using decorativeplant_be.Application.Common.DTOs.Common;
using decorativeplant_be.Application.Common.DTOs.Garden;
using decorativeplant_be.Application.Features.Inventory.Commands;
using decorativeplant_be.Application.Features.Inventory.DTOs;
using decorativeplant_be.Application.Features.Inventory.Queries;
using decorativeplant_be.Application.Features.PlantLibrary.DTOs;
using decorativeplant_be.Application.Features.Branch.Queries;
using decorativeplant_be.Application.Features.Branch.DTOs;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace decorativeplant_be.API.Controllers;

[ApiController]
[Route("api/inventory")]
[Authorize] // Require staff/admin
public class InventoryController : BaseController
{
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

    /// <summary>
    /// Get stock transfer detail by ID.
    /// </summary>
    [HttpGet("transfers/{id}")]
    public async Task<ActionResult<ApiResponse<StockTransferDto>>> GetTransfer(Guid id)
    {
        var result = await Mediator.Send(new GetStockTransferByIdQuery(id));
        if (result == null) return NotFound(ApiResponse<StockTransferDto>.ErrorResponse("Transfer not found.", null, 404));
        return Ok(ApiResponse<StockTransferDto>.SuccessResponse(result));
    }

    /// <summary>
    /// Get items with low stock.
    /// </summary>
    [HttpGet("low-stock")]
    [Authorize(Roles = "admin,branch_manager,store_staff,cultivation_staff,fulfillment_staff")]
    public async Task<ActionResult<ApiResponse<List<LowStockItemDto>>>> GetLowStock([FromQuery] Guid? branchId, [FromQuery] int threshold = 10)
    {
        var query = new GetLowStockQuery { BranchId = branchId, Threshold = threshold };
        var result = await Mediator.Send(query);
        
        // DEBUG: Log the serialized JSON to see the naming case
        foreach(var item in result) {
            Console.WriteLine($"DEBUG-Inventory: ProductId={item.ProductId}, ProductName={item.ProductName}, Price={item.Price}, Category={item.Category}");
        }
        var json = System.Text.Json.JsonSerializer.Serialize(ApiResponse<List<LowStockItemDto>>.SuccessResponse(result));
        Console.WriteLine($"DEBUG-Inventory-JSON: {json}");

        return Ok(ApiResponse<List<LowStockItemDto>>.SuccessResponse(result));
    }

    /// <summary>
    /// Check product availability.
    /// </summary>
    [HttpGet("availability/{productListingId}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<ProductAvailabilityDto>>> GetAvailability(Guid productListingId)
    {
        var query = new GetProductAvailabilityQuery { ProductListingId = productListingId };
        var result = await Mediator.Send(query);
        return Ok(ApiResponse<ProductAvailabilityDto>.SuccessResponse(result));
    }

    /// <summary>
    /// Get all branches for inventory selection.
    /// </summary>
    [HttpGet("branches")]
    public async Task<ActionResult<ApiResponse<List<BranchDto>>>> GetBranches()
    {
        var result = await Mediator.Send(new GetAllBranchesQuery());
        return Ok(ApiResponse<List<BranchDto>>.SuccessResponse(result));
    }
}
