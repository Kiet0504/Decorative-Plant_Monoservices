using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using decorativeplant_be.Application.Common.DTOs.Commerce;
using decorativeplant_be.Application.Common.DTOs.Common;
using decorativeplant_be.Application.Common.Interfaces;
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
        var isAdmin = User.IsInRole("admin");
        var userId = isAdmin ? null : GetUserId();
        var result = await Mediator.Send(new GetOrdersQuery { UserId = userId, BranchId = branchId, Status = status, Page = page, PageSize = pageSize });
        return Ok(ApiResponse<PagedResult<OrderResponse>>.SuccessResponse(result));
    }

    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> GetById(Guid id)
    {
        var isAdmin = User.IsInRole("admin");
        var userId = isAdmin ? (Guid?)null : GetUserId();
        var result = await Mediator.Send(new GetOrderByIdQuery { Id = id, UserId = userId });
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

    [HttpGet("ghn/provinces")]
    [Authorize]
    public async Task<IActionResult> GetProvinces([FromServices] decorativeplant_be.Application.Common.Interfaces.IShippingService shipping)
    {
        var result = await shipping.GetProvincesAsync();
        return Ok(ApiResponse<List<GhnProvince>>.SuccessResponse(result));
    }

    [HttpGet("ghn/districts")]
    [Authorize]
    public async Task<IActionResult> GetDistricts([FromQuery] int provinceId, [FromServices] decorativeplant_be.Application.Common.Interfaces.IShippingService shipping)
    {
        var result = await shipping.GetDistrictsAsync(provinceId);
        return Ok(ApiResponse<List<GhnDistrict>>.SuccessResponse(result));
    }

    [HttpGet("ghn/wards")]
    [Authorize]
    public async Task<IActionResult> GetWards([FromQuery] int districtId, [FromServices] decorativeplant_be.Application.Common.Interfaces.IShippingService shipping)
    {
        var result = await shipping.GetWardsAsync(districtId);
        return Ok(ApiResponse<List<GhnWard>>.SuccessResponse(result));
    }

    [HttpGet("{id:guid}/tracking")]
    [Authorize]
    public async Task<IActionResult> GetTracking(
        Guid id,
        [FromServices] IApplicationDbContext context,
        [FromServices] IShippingService shipping)
    {
        var order = await context.OrderHeaders
            .Include(o => o.OrderItems)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null) return NotFound(ApiResponse<object>.ErrorResponse("Order not found", statusCode: 404));

        var userId = GetUserId();
        var isAdmin = User.IsInRole("admin");
        var isStaff = User.IsInRole("store_staff") || User.IsInRole("branch_manager");

        Guid? staffBranchId = null;

        if (!isAdmin)
        {
            if (isStaff)
            {
                staffBranchId = await context.StaffAssignments
                    .Where(s => s.StaffId == userId && s.IsPrimary)
                    .Select(s => (Guid?)s.BranchId)
                    .FirstOrDefaultAsync();

                var hasItemsFromBranch = staffBranchId.HasValue
                    && order.OrderItems.Any(oi => oi.BranchId == staffBranchId);

                if (!hasItemsFromBranch) return Forbid();
            }
            else
            {
                if (order.UserId != userId) return Forbid();
            }
        }

        var results = new List<GhnTrackingResponse>();

        if (order.Notes?.RootElement.TryGetProperty("shipments", out var shipments) == true
            && shipments.ValueKind == JsonValueKind.Array)
        {
            foreach (var shipment in shipments.EnumerateArray())
            {
                if (!shipment.TryGetProperty("tracking_code", out var tc)) continue;
                var code = tc.GetString();
                if (string.IsNullOrEmpty(code)) continue;

                // Staff only sees shipments belonging to their branch
                if (isStaff)
                {
                    var shipmentBranchId = shipment.TryGetProperty("branch_id", out var bid)
                        ? bid.GetString() : null;
                    if (shipmentBranchId != staffBranchId?.ToString()) continue;
                }

                var tracking = await shipping.TrackOrderAsync(code);
                if (tracking != null)
                {
                    tracking.Carrier = shipment.TryGetProperty("carrier", out var c) ? c.GetString() : "GHN";
                    tracking.BranchId = shipment.TryGetProperty("branch_id", out var b) ? b.GetString() : null;
                    results.Add(tracking);
                }
            }
        }

        return Ok(ApiResponse<List<GhnTrackingResponse>>.SuccessResponse(results));
    }

    [HttpPost("{id:guid}/tracking/switch-status")]
    [Authorize(Roles = "store_staff,branch_manager,admin")]
    public async Task<IActionResult> SwitchGhnStatus(
        Guid id,
        [FromBody] SwitchGhnStatusRequest request,
        [FromServices] IApplicationDbContext context,
        [FromServices] IShippingService shipping)
    {
        var validStatuses = new HashSet<string>
        {
            "ready_to_pick", "picking", "picked",
            "storing", "sorting", "transporting", "delivering",
            "delivered",
            "delivery_fail", "waiting_to_return", "return", "returned",
            "cancel", "lost"
        };
        if (!validStatuses.Contains(request.TargetStatus))
            return BadRequest(ApiResponse<object>.ErrorResponse("Invalid target status"));

        var order = await context.OrderHeaders
            .Include(o => o.OrderItems)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null) return NotFound(ApiResponse<object>.ErrorResponse("Order not found", statusCode: 404));

        var userId = GetUserId();
        var isAdmin = User.IsInRole("admin");

        Guid? staffBranchId = null;
        if (!isAdmin)
        {
            staffBranchId = await context.StaffAssignments
                .Where(s => s.StaffId == userId && s.IsPrimary)
                .Select(s => (Guid?)s.BranchId)
                .FirstOrDefaultAsync();

            var hasItemsFromBranch = staffBranchId.HasValue
                && order.OrderItems.Any(oi => oi.BranchId == staffBranchId);
            if (!hasItemsFromBranch) return Forbid();
        }

        if (order.Notes?.RootElement.TryGetProperty("shipments", out var shipments) != true
            || shipments.ValueKind != JsonValueKind.Array)
            return BadRequest(ApiResponse<object>.ErrorResponse("No GHN shipments found for this order"));

        var switched = new List<string>();
        foreach (var shipment in shipments.EnumerateArray())
        {
            if (!shipment.TryGetProperty("tracking_code", out var tc)) continue;
            var code = tc.GetString();
            if (string.IsNullOrEmpty(code)) continue;

            if (!isAdmin)
            {
                var shipmentBranchId = shipment.TryGetProperty("branch_id", out var bid) ? bid.GetString() : null;
                if (shipmentBranchId != staffBranchId?.ToString()) continue;
            }

            var ok = await shipping.SwitchGhnStatusAsync(code, request.TargetStatus);
            if (ok) switched.Add(code);
        }

        if (switched.Count == 0)
            return BadRequest(ApiResponse<object>.ErrorResponse("Failed to switch status on GHN"));

        // GHN switched successfully — now mirror the change into our own DB so FE
        // stepper/timeline reflect reality without staff having to bump PATCH /status too.
        var shippingRows = await context.Shippings
            .Where(s => s.OrderId == order.Id && s.TrackingCode != null && switched.Contains(s.TrackingCode))
            .ToListAsync();

        foreach (var s in shippingRows)
            s.Status = request.TargetStatus;

        var mappedOrderStatus = MapGhnStatusToOrderStatus(request.TargetStatus);
        if (mappedOrderStatus != null)
            order.Status = mappedOrderStatus;

        await context.SaveChangesAsync(CancellationToken.None);

        return Ok(ApiResponse<object>.SuccessResponse(
            new { switched, orderStatus = order.Status },
            $"Switched {switched.Count} shipment(s) to '{request.TargetStatus}'"));
    }

    // GHN real flow: ready_to_pick → picking → picked → storing → sorting → transporting → delivering → delivered
    private static string? MapGhnStatusToOrderStatus(string ghnStatus) => ghnStatus switch
    {
        "ready_to_pick"                                  => "confirmed",
        "picking" or "picked" or "storing" or "sorting"  => "processing",
        "transporting" or "delivering" or "delivery_fail" => "shipping",
        "delivered"                                      => "delivered",
        "waiting_to_return" or "return" or "returned"    => "returned",
        "cancel" or "lost"                               => "cancelled",
        _                                                => null,
    };

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Create([FromBody] CreateOrderRequest request)
    {
        var result = await Mediator.Send(new CreateOrderCommand { UserId = GetUserId(), Request = request });
        return Ok(ApiResponse<List<OrderResponse>>.SuccessResponse(result, "Orders created", 201));
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = "admin,store_staff,branch_manager")]
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

    [HttpPost("{id:guid}/confirm-receipt")]
    [Authorize]
    public async Task<IActionResult> ConfirmReceipt(Guid id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var result = await Mediator.Send(new ConfirmReceiptCommand { OrderId = id, UserId = userId.Value });
        return Ok(ApiResponse<OrderResponse>.SuccessResponse(result, "Order confirmed as received"));
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
