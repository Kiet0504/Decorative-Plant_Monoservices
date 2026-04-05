using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using decorativeplant_be.Application.Common.DTOs.Commerce;
using decorativeplant_be.Application.Common.DTOs.Common;
using decorativeplant_be.Application.Features.Commerce.Orders.Commands;
using decorativeplant_be.Application.Features.Commerce.Orders.Queries;
using Microsoft.AspNetCore.RateLimiting;

namespace decorativeplant_be.API.Controllers;

[Route("api/v{version:apiVersion}/orders")]
[EnableRateLimiting("CartAndOrderPolicy")]
public class OrdersController : BaseController
{
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetOrders([FromQuery] Guid? branchId, [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await Mediator.Send(new GetOrdersQuery { UserId = GetUserId(), BranchId = branchId, Status = status, Page = page, PageSize = pageSize });
        return Ok(ApiResponse<PagedResult<OrderResponse>>.SuccessResponse(result));
    }

    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await Mediator.Send(new GetOrderByIdQuery { Id = id });
        if (result == null) return NotFound(ApiResponse<object>.ErrorResponse("Order not found", statusCode: 404));
        return Ok(ApiResponse<OrderResponse>.SuccessResponse(result));
    }

    [HttpGet("shipping-fee")]
    [Authorize]
    public async Task<IActionResult> GetShippingFee(
        [FromQuery] int fromDistrictId = 3695,
        [FromQuery] string fromWardCode = "90737",
        [FromQuery] int toDistrictId = 1454,
        [FromQuery] string toWardCode = "21211",
        [FromQuery] int weight = 1000,
        [FromQuery] int insuranceValue = 500000)
    {
        var result = await Mediator.Send(new GetShippingFeeQuery(fromDistrictId, fromWardCode, toDistrictId, toWardCode, weight, insuranceValue));
        return Ok(ApiResponse<ShippingFeeResponse>.SuccessResponse(result));
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Create([FromBody] CreateOrderRequest request)
    {
        var result = await Mediator.Send(new CreateOrderCommand { UserId = GetUserId(), Request = request });
        return Ok(ApiResponse<List<OrderResponse>>.SuccessResponse(result, "Orders created", 201));
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateOrderStatusRequest request)
    {
        var result = await Mediator.Send(new UpdateOrderStatusCommand { Id = id, Request = request });
        return Ok(ApiResponse<OrderResponse>.SuccessResponse(result));
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelOrderRequest request)
    {
        var result = await Mediator.Send(new CancelOrderCommand { Id = id, UserId = GetUserId(), Request = request });
        return Ok(ApiResponse<OrderResponse>.SuccessResponse(result));
    }

    [HttpPost("offline-bopis-request")]
    [Authorize(Roles = "BrandManager,Admin")]
    public async Task<IActionResult> CreateOfflineBopis([FromBody] CreateOfflineBopisRequest request)
    {
        if (GetUserId() == null) return Unauthorized();
        var result = await Mediator.Send(new CreateOfflineBopisOrderCommand { BrandManagerId = GetUserId()!.Value, Request = request });
        return Ok(ApiResponse<OrderResponse>.SuccessResponse(result, "Offline BOPIS request created successfully", 201));
    }
}
