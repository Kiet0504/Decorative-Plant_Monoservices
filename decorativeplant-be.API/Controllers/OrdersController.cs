using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using decorativeplant_be.Application.Common.DTOs.Commerce;
using decorativeplant_be.Application.Common.DTOs.Common;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Commerce.Orders.Commands;
using decorativeplant_be.Application.Features.Commerce.Orders.Queries;
using decorativeplant_be.Application.Services;
using decorativeplant_be.Domain.Entities;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using decorativeplant_be.Infrastructure.Ghn;
using decorativeplant_be.Infrastructure.Ghtk;

namespace decorativeplant_be.API.Controllers;

[Route("api/v{version:apiVersion}/orders")]
[EnableRateLimiting("CartAndOrderPolicy")]
public class OrdersController : BaseController
{
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetOrders(
        [FromQuery] Guid? branchId,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromServices] IApplicationDbContext context = null!)
    {
        var isAdmin = User.IsInRole("admin");
        var isStaff = User.IsInRole("store_staff") || User.IsInRole("branch_manager") || User.IsInRole("fulfillment_staff");

        Guid? userId = null;
        Guid? effectiveBranchId = branchId;

        if (isAdmin)
        {
            // Admin sees everything — no userId / branchId filter unless explicitly passed
        }
        else if (isStaff)
        {
            
            if (!effectiveBranchId.HasValue)
            {
                var staffUserId = GetUserId();
                effectiveBranchId = await context.StaffAssignments
                    .Where(s => s.StaffId == staffUserId && s.IsPrimary)
                    .Select(s => (Guid?)s.BranchId)
                    .FirstOrDefaultAsync();
            }
        }
        else
        {
            // Regular customer — only see their own orders
            userId = GetUserId();
        }

        var result = await Mediator.Send(new GetOrdersQuery
        {
            UserId = userId,
            BranchId = effectiveBranchId,
            Status = status,
            Page = page,
            PageSize = pageSize
        });
        return Ok(ApiResponse<PagedResult<OrderResponse>>.SuccessResponse(result));
    }

    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> GetById(Guid id)
    {
        var isAdmin = User.IsInRole("admin");
        var isStaff = User.IsInRole("store_staff") || User.IsInRole("branch_manager") || User.IsInRole("fulfillment_staff");
        var userId = (isAdmin || isStaff) ? (Guid?)null : GetUserId();
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
        var isStaff = User.IsInRole("store_staff") || User.IsInRole("branch_manager") || User.IsInRole("fulfillment_staff");

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
                // If shipment has no branch_id (null), show it to all staff
                if (isStaff && staffBranchId.HasValue)
                {
                    var shipmentBranchId = shipment.TryGetProperty("branch_id", out var bid)
                        ? bid.GetString() : null;
                    if (shipmentBranchId != null && shipmentBranchId != staffBranchId.Value.ToString()) continue;
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
    [Authorize(Roles = "store_staff,branch_manager,fulfillment_staff,admin")]
    public async Task<IActionResult> SwitchGhnStatus(
        Guid id,
        [FromBody] SwitchGhnStatusRequest request,
        [FromServices] IApplicationDbContext context,
        [FromServices] IShippingService shipping,
        [FromServices] IStockService stockService)
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
        var logger = HttpContext.RequestServices.GetRequiredService<ILogger<OrdersController>>();
        logger.LogInformation("SwitchGhnStatus: orderId={OrderId}, isAdmin={IsAdmin}, staffBranchId={StaffBranchId}, targetStatus={Target}, shipmentCount={Count}",
            id, isAdmin, staffBranchId, request.TargetStatus, shipments.GetArrayLength());

        foreach (var shipment in shipments.EnumerateArray())
        {
            if (!shipment.TryGetProperty("tracking_code", out var tc)) { logger.LogWarning("Shipment missing tracking_code, skipping"); continue; }
            var code = tc.GetString();
            if (string.IsNullOrEmpty(code)) { logger.LogWarning("Shipment has empty tracking_code, skipping"); continue; }

            if (!isAdmin && staffBranchId.HasValue)
            {
                var shipmentBranchId = shipment.TryGetProperty("branch_id", out var bid) ? bid.GetString() : null;
                if (shipmentBranchId != null && shipmentBranchId != staffBranchId.Value.ToString())
                {
                    logger.LogWarning("Shipment {Code} branch mismatch: {ShipBranch} != {StaffBranch}, skipping", code, shipmentBranchId, staffBranchId);
                    continue;
                }
            }

            logger.LogInformation("Calling GHN switch-status for {Code} → {Target}", code, request.TargetStatus);
            var ok = await shipping.SwitchGhnStatusAsync(code, request.TargetStatus);
            logger.LogInformation("GHN switch-status result for {Code}: {Result}", code, ok);
            if (ok) switched.Add(code);
        }

        // GHN shop/client tokens are only authorized to set a subset of statuses
        // (e.g. storing, cancel, return). Picking/transporting/delivered are shipper-only
        // and come back via webhook. If every GHN call failed, fall back to a local-only
        // transition so staff can still mark the order manually — GHN will mirror later
        // via the webhook with no double-apply (ApplyFromExternalSource is a no-op on
        // same-state transitions and we pin order.Status below).
        var ghnAllSucceeded = switched.Count > 0;
        var mappedOrderStatus = MapGhnStatusToOrderStatus(request.TargetStatus);

        if (!ghnAllSucceeded)
        {
            logger.LogWarning(
                "SwitchGhnStatus: GHN rejected all shipments for order {OrderId} → '{Target}'. Falling back to local-only transition.",
                order.Id, request.TargetStatus);

            if (mappedOrderStatus == null)
                return BadRequest(ApiResponse<object>.ErrorResponse(
                    $"GHN rejected the request and '{request.TargetStatus}' has no local equivalent."));

            if (!decorativeplant_be.Application.Features.Commerce.Orders
                    .OrderStatusMachine.CanTransition(order.Status, mappedOrderStatus))
            {
                return BadRequest(ApiResponse<object>.ErrorResponse(
                    $"GHN rejected the request and local transition '{order.Status}' → '{mappedOrderStatus}' is not allowed."));
            }

            decorativeplant_be.Application.Features.Commerce.Orders.OrderStatusMachine
                .Apply(order, mappedOrderStatus, changedBy: userId,
                    reason: $"GHN reject fallback (target: {request.TargetStatus})",
                    source: "StaffLocalOverride");

            // Mirror the override into Shipping rows so the customer-facing GHN Tracking
            // stepper matches the top-level order stepper. Append an event so the audit
            // trail preserves the fact that this was a local override, not a real GHN push.
            var shippingRowsFallback = await context.Shippings
                .Where(s => s.OrderId == order.Id)
                .ToListAsync();
            foreach (var s in shippingRowsFallback)
            {
                s.Status = request.TargetStatus;

                var events = new List<object?>();
                if (s.Events != null && s.Events.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var el in s.Events.RootElement.EnumerateArray())
                        events.Add(JsonSerializer.Deserialize<object?>(el.GetRawText()));
                }
                events.Add(new
                {
                    status = request.TargetStatus,
                    at = DateTime.UtcNow.ToString("o"),
                    source = "StaffLocalOverride",
                    note = "GHN rejected switch (shop token); mirrored locally so UI matches."
                });
                s.Events = JsonDocument.Parse(JsonSerializer.Serialize(events));
            }

            await context.SaveChangesAsync(CancellationToken.None);

            return Ok(ApiResponse<object>.SuccessResponse(
                new { switched, orderStatus = order.Status, ghnSynced = false },
                $"GHN did not accept the change (shop-token permission). Applied local status '{order.Status}' instead."));
        }

        // GHN switched successfully — now mirror the change into our own DB so FE
        // stepper/timeline reflect reality without staff having to bump PATCH /status too.
        var shippingRows = await context.Shippings
            .Where(s => s.OrderId == order.Id && s.TrackingCode != null && switched.Contains(s.TrackingCode))
            .ToListAsync();

        foreach (var s in shippingRows)
            s.Status = request.TargetStatus;

        if (mappedOrderStatus != null)
        {
            var wasTerminalBefore = decorativeplant_be.Application.Features.Commerce.Orders
                .OrderStatusMachine.IsTerminal(order.Status);

            decorativeplant_be.Application.Features.Commerce.Orders.OrderStatusMachine
                .ApplyFromExternalSource(order, mappedOrderStatus, source: "GHN",
                    reason: $"GHN status: {request.TargetStatus}");

            if (!wasTerminalBefore &&
                (mappedOrderStatus == decorativeplant_be.Application.Features.Commerce.Orders.OrderStatusMachine.Returned ||
                 mappedOrderStatus == decorativeplant_be.Application.Features.Commerce.Orders.OrderStatusMachine.Cancelled) &&
                order.OrderItems != null && order.OrderItems.Count > 0)
            {
                try
                {
                    await stockService.RestoreOrderStockAsync(order.OrderItems, CancellationToken.None);
                    logger.LogInformation("SwitchGhnStatus: restored stock for order {OrderId} on status {Mapped}.",
                        order.Id, mappedOrderStatus);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "SwitchGhnStatus: failed to restore stock for order {OrderId}.", order.Id);
                }
            }
        }

        await context.SaveChangesAsync(CancellationToken.None);

        return Ok(ApiResponse<object>.SuccessResponse(
            new { switched, orderStatus = order.Status, ghnSynced = true },
            $"Switched {switched.Count} shipment(s) to '{request.TargetStatus}'"));
    }

    // GHN real flow: ready_to_pick → picking → picked → storing → sorting → transporting → delivering → delivered
    // Business mapping (customer-visible labels in parentheses):
    //   - ready_to_pick, picking: GHN order created, shipper not yet handed the package → `processing` (Đang chờ lấy hàng)
    //   - picked+: package is physically in GHN custody → `shipping` (Chờ giao hàng)
    //   - delivered: dropped at customer → `delivered` (Đã giao)
    // Note: `confirmed` (Đã xác nhận) is set by the staff confirm action or payment success, not by any GHN state.
    private static string? MapGhnStatusToOrderStatus(string ghnStatus) => ghnStatus switch
    {
        "ready_to_pick" or "picking"                                                             => "processing",
        "picked" or "storing" or "sorting" or "transporting" or "delivering" or "delivery_fail"  => "shipping",
        "delivered"                                                                              => "delivered",
        "waiting_to_return" or "return" or "returned"                                            => "returned",
        "cancel" or "lost"                                                                       => "cancelled",
        _                                                                                        => null,
    };

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Create([FromBody] CreateOrderRequest request)
    {
        var result = await Mediator.Send(new CreateOrderCommand { UserId = GetUserId(), Request = request });
        return Ok(ApiResponse<List<OrderResponse>>.SuccessResponse(result, "Orders created", 201));
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = "admin,store_staff,branch_manager,fulfillment_staff")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateOrderStatusRequest request)
    {
        var result = await Mediator.Send(new UpdateOrderStatusCommand { Id = id, ActorUserId = GetUserId(), Request = request });
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

    /// <summary>
    /// Webhook endpoint GHN calls when shipment status changes.
    /// Authenticated via the shared "Token" header (configured in GHN's Hook Orders panel).
    /// Rate limiter is disabled so GHN retry bursts are not dropped.
    /// Payload (minimal): { OrderCode, Status, Description } where OrderCode is the GHN tracking code.
    /// </summary>
    [HttpPost("ghn/webhook")]
    [AllowAnonymous]
    [DisableRateLimiting]
    public async Task<IActionResult> GhnWebhook(
        [FromBody] GhnWebhookRequest payload,
        [FromServices] IApplicationDbContext context,
        [FromServices] IStockService stockService,
        [FromServices] IOptions<GhnSettings> ghnOptions,
        [FromServices] ILogger<OrdersController> logger)
    {
        // 1) Token header check. Empty config = disabled (dev only).
        var expected = ghnOptions.Value.WebhookToken;
        if (!string.IsNullOrWhiteSpace(expected))
        {
            var provided = Request.Headers["Token"].ToString();
            if (!string.Equals(provided, expected, StringComparison.Ordinal))
            {
                logger.LogWarning("GHN webhook rejected: invalid or missing Token header.");
                return Unauthorized(ApiResponse<object>.ErrorResponse("Invalid webhook token", statusCode: 401));
            }
        }

        if (string.IsNullOrWhiteSpace(payload.OrderCode) || string.IsNullOrWhiteSpace(payload.Status))
            return BadRequest(ApiResponse<object>.ErrorResponse("Missing OrderCode or Status"));

        // 2) Find the OrderHeader. Primary lookup: Shipping.TrackingCode row.
        var shipping = await context.Shippings
            .FirstOrDefaultAsync(s => s.TrackingCode == payload.OrderCode);

        OrderHeader? order = null;
        if (shipping?.OrderId != null)
        {
            order = await context.OrderHeaders.Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.Id == shipping.OrderId);
        }

        if (order == null)
        {
            // Fallback: scan Notes.shipments[].tracking_code in-app (should rarely fire).
            var all = await context.OrderHeaders
                .Where(o => o.Notes != null)
                .ToListAsync();
            order = all.FirstOrDefault(o =>
                o.Notes != null &&
                o.Notes.RootElement.TryGetProperty("shipments", out var sh) &&
                sh.ValueKind == JsonValueKind.Array &&
                sh.EnumerateArray().Any(s =>
                    s.TryGetProperty("tracking_code", out var tc) &&
                    tc.GetString() == payload.OrderCode));
        }

        if (order == null)
            return NotFound(ApiResponse<object>.ErrorResponse("Order for tracking code not found"));

        // 3) Idempotency guards.
        //    (a) Shipping row status already matches → GHN is retrying. Ack without writing.
        if (shipping != null && string.Equals(shipping.Status, payload.Status, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogInformation("GHN webhook duplicate for tracking {Code} status {Status} — no-op.",
                payload.OrderCode, payload.Status);
            return Ok(ApiResponse<object>.SuccessResponse(new { orderId = order.Id, status = order.Status, duplicate = true }));
        }

        if (shipping != null) shipping.Status = payload.Status;

        var mapped = MapGhnStatusToOrderStatus(payload.Status);
        if (mapped == null)
        {
            // Unknown/unmapped GHN state — log and persist the shipping row update only.
            logger.LogInformation("GHN webhook: status {Status} has no order-status mapping, keeping order.status={Order}.",
                payload.Status, order.Status);
        }
        //    (b) Mapped status equals current order status → append nothing (no duplicate history entry).
        else if (!string.Equals(order.Status, mapped, StringComparison.OrdinalIgnoreCase))
        {
            var wasTerminalBefore = decorativeplant_be.Application.Features.Commerce.Orders
                .OrderStatusMachine.IsTerminal(order.Status);

            decorativeplant_be.Application.Features.Commerce.Orders.OrderStatusMachine
                .ApplyFromExternalSource(order, mapped, source: "GhnWebhook",
                    reason: payload.Description ?? payload.Status);

            // COD settlement: when the shipper marks the parcel delivered and this is a
            // COD order, insert a one-time PaymentTransaction so the order is recorded as
            // paid. Guarded by payment_method and a dedupe check — GHN retries the webhook.
            if (mapped == decorativeplant_be.Application.Features.Commerce.Orders.OrderStatusMachine.Delivered)
            {
                var isCod = order.TypeInfo != null
                    && order.TypeInfo.RootElement.TryGetProperty("payment_method", out var pm)
                    && string.Equals(pm.GetString(), "cod", StringComparison.OrdinalIgnoreCase);
                if (isCod)
                {
                    var alreadyPaid = await context.PaymentTransactions
                        .AnyAsync(p => p.OrderId == order.Id
                            && p.Details != null
                            && EF.Functions.JsonContains(p.Details, "{\"method\":\"cod\",\"status\":\"success\"}"));
                    if (!alreadyPaid)
                    {
                        var totalStr = order.Financials?.RootElement.TryGetProperty("total", out var tot) == true
                            ? tot.GetString() ?? "0" : "0";
                        var codTx = new PaymentTransaction
                        {
                            Id = Guid.NewGuid(),
                            OrderId = order.Id,
                            TransactionCode = $"COD-{Guid.NewGuid().ToString()[..8].ToUpper()}",
                            Details = JsonDocument.Parse(JsonSerializer.Serialize(new
                            {
                                provider = "cash",
                                method = "cod",
                                type = "payment",
                                amount = totalStr,
                                status = "success",
                                external_id = payload.OrderCode,
                                collected_at = DateTime.UtcNow
                            })),
                            CreatedAt = DateTime.UtcNow
                        };
                        context.PaymentTransactions.Add(codTx);
                        logger.LogInformation("GHN webhook: recorded COD settlement for order {OrderId} amount {Amount}.",
                            order.Id, totalStr);
                    }
                }
            }

            // Restore stock if GHN tells us the order ended without delivery and we hadn't
            // already closed it locally (avoids double-restore after customer cancel / expiry).
            if (!wasTerminalBefore &&
                (mapped == decorativeplant_be.Application.Features.Commerce.Orders.OrderStatusMachine.Returned ||
                 mapped == decorativeplant_be.Application.Features.Commerce.Orders.OrderStatusMachine.Cancelled) &&
                order.OrderItems != null && order.OrderItems.Count > 0)
            {
                try
                {
                    await stockService.RestoreOrderStockAsync(order.OrderItems, CancellationToken.None);
                    logger.LogInformation("GHN webhook: restored stock for order {OrderId} on status {Mapped}.",
                        order.Id, mapped);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "GHN webhook: failed to restore stock for order {OrderId}.", order.Id);
                }

                if (order.VoucherId.HasValue)
                {
                    try
                    {
                        await decorativeplant_be.Application.Features.Commerce.Vouchers.VoucherUsageHelper
                            .RollbackUsageAsync(context, order.VoucherId.Value, CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "GHN webhook: failed to rollback voucher usage for order {OrderId}.", order.Id);
                    }
                }
            }
        }

        await context.SaveChangesAsync(CancellationToken.None);
        return Ok(ApiResponse<object>.SuccessResponse(new { orderId = order.Id, status = order.Status }));
    }

    public class GhnWebhookRequest
    {
        public string OrderCode { get; set; } = string.Empty; // GHN tracking code
        public string Status { get; set; } = string.Empty;    // GHN status name
        public string? Description { get; set; }
    }

    /// <summary>
    /// Webhook endpoint GHTK calls on shipment status changes.
    /// Docs: https://api.ghtk.vn/docs/submit-order/logistic-overview (webhook section).
    /// Authenticated via shared <c>X-Secure-Token</c> header (configured in GHTK dashboard).
    /// GHTK posts numeric <c>status_id</c> plus <c>label_id</c> / <c>partner_id</c> (our client order id).
    /// </summary>
    [HttpPost("ghtk/webhook")]
    [AllowAnonymous]
    [DisableRateLimiting]
    public async Task<IActionResult> GhtkWebhook(
        [FromBody] GhtkWebhookRequest payload,
        [FromServices] IApplicationDbContext context,
        [FromServices] IStockService stockService,
        [FromServices] IOptions<GhtkSettings> ghtkOptions,
        [FromServices] ILogger<OrdersController> logger)
    {
        // 1) Shared-secret header check (empty config disables — dev only).
        var expected = ghtkOptions.Value.WebhookToken;
        if (!string.IsNullOrWhiteSpace(expected))
        {
            var provided = Request.Headers["X-Secure-Token"].ToString();
            if (string.IsNullOrEmpty(provided))
                provided = Request.Headers["Token"].ToString(); // fallback alias
            if (!string.Equals(provided, expected, StringComparison.Ordinal))
            {
                logger.LogWarning("GHTK webhook rejected: invalid or missing X-Secure-Token header.");
                return Unauthorized(ApiResponse<object>.ErrorResponse("Invalid webhook token", statusCode: 401));
            }
        }

        var trackingCode = payload.LabelId ?? payload.TrackingId;
        if (string.IsNullOrWhiteSpace(trackingCode) && string.IsNullOrWhiteSpace(payload.PartnerId))
            return BadRequest(ApiResponse<object>.ErrorResponse("Missing label_id / tracking_id / partner_id"));

        // 2) Locate Shipping row by GHTK tracking code, or fall back to our client order id (partner_id).
        Shipping? shipping = null;
        if (!string.IsNullOrWhiteSpace(trackingCode))
        {
            shipping = await context.Shippings.FirstOrDefaultAsync(s => s.TrackingCode == trackingCode);
        }

        OrderHeader? order = null;
        if (shipping?.OrderId != null)
        {
            order = await context.OrderHeaders.Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.Id == shipping.OrderId);
        }
        else if (!string.IsNullOrWhiteSpace(payload.PartnerId) && Guid.TryParse(payload.PartnerId, out var pid))
        {
            order = await context.OrderHeaders.Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.Id == pid);
            if (order != null && shipping == null)
            {
                shipping = await context.Shippings.FirstOrDefaultAsync(s => s.OrderId == order.Id);
            }
        }

        if (order == null)
            return NotFound(ApiResponse<object>.ErrorResponse("Order for GHTK callback not found"));

        // 3) Map GHTK numeric status → local order status. Unknown codes: ack without writing history.
        var mapped = MapGhtkStatusToOrderStatus(payload.StatusId);
        var shippingStatusText = payload.StatusText ?? payload.StatusId.ToString();

        // Idempotency: if the Shipping row already has this status, it's a GHTK retry — ack-only.
        if (shipping != null && string.Equals(shipping.Status, shippingStatusText, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogInformation("GHTK webhook duplicate for tracking {Code} status {Status} — no-op.",
                trackingCode, shippingStatusText);
            return Ok(ApiResponse<object>.SuccessResponse(new { orderId = order.Id, status = order.Status, duplicate = true }));
        }

        if (shipping != null) shipping.Status = shippingStatusText;

        if (mapped != null && !string.Equals(order.Status, mapped, StringComparison.OrdinalIgnoreCase))
        {
            var wasTerminalBefore = decorativeplant_be.Application.Features.Commerce.Orders
                .OrderStatusMachine.IsTerminal(order.Status);

            decorativeplant_be.Application.Features.Commerce.Orders.OrderStatusMachine
                .ApplyFromExternalSource(order, mapped, source: "GhtkWebhook",
                    reason: payload.Reason ?? payload.StatusText ?? $"GHTK status {payload.StatusId}");

            // Restore stock if GHTK ended the order without delivery and we hadn't already closed locally.
            if (!wasTerminalBefore &&
                (mapped == decorativeplant_be.Application.Features.Commerce.Orders.OrderStatusMachine.Returned ||
                 mapped == decorativeplant_be.Application.Features.Commerce.Orders.OrderStatusMachine.Cancelled) &&
                order.OrderItems != null && order.OrderItems.Count > 0)
            {
                try
                {
                    await stockService.RestoreOrderStockAsync(order.OrderItems, CancellationToken.None);
                    logger.LogInformation("GHTK webhook: restored stock for order {OrderId} on status {Mapped}.",
                        order.Id, mapped);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "GHTK webhook: failed to restore stock for order {OrderId}.", order.Id);
                }
            }
        }
        else if (mapped == null)
        {
            logger.LogInformation("GHTK webhook: status_id {Status} has no order-status mapping, keeping order.status={Order}.",
                payload.StatusId, order.Status);
        }

        await context.SaveChangesAsync(CancellationToken.None);
        return Ok(ApiResponse<object>.SuccessResponse(new { orderId = order.Id, status = order.Status }));
    }

    /// <summary>
    /// GHTK numeric status codes → local order state. Reference codes from
    /// https://api.ghtk.vn/docs/submit-order/logistic-overview (status table).
    /// </summary>
    private static string? MapGhtkStatusToOrderStatus(int statusId) => statusId switch
    {
        -1 => decorativeplant_be.Application.Features.Commerce.Orders.OrderStatusMachine.Cancelled, // hủy đơn
        1  => decorativeplant_be.Application.Features.Commerce.Orders.OrderStatusMachine.Pending,    // chưa tiếp nhận
        2  => decorativeplant_be.Application.Features.Commerce.Orders.OrderStatusMachine.Confirmed,  // đã tiếp nhận
        // picked / in warehouse / sorting → internal processing
        3 or 4 or 11 or 123 => decorativeplant_be.Application.Features.Commerce.Orders.OrderStatusMachine.Processing,
        5 => decorativeplant_be.Application.Features.Commerce.Orders.OrderStatusMachine.Shipping,    // đang giao
        6 => decorativeplant_be.Application.Features.Commerce.Orders.OrderStatusMachine.Delivered,   // giao thành công
        // Returns: 7 = không lấy được hàng; 9 = không giao được; 12/13/20/21 = return flow
        7 or 9 or 12 or 13 or 20 or 21 => decorativeplant_be.Application.Features.Commerce.Orders.OrderStatusMachine.Returned,
        _ => null
    };

    public class GhtkWebhookRequest
    {
        [System.Text.Json.Serialization.JsonPropertyName("label_id")]
        public string? LabelId { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("tracking_id")]
        public string? TrackingId { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("partner_id")]
        public string? PartnerId { get; set; } // our client order id (Guid string)
        [System.Text.Json.Serialization.JsonPropertyName("status_id")]
        public int StatusId { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("action_time")]
        public string? ActionTime { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("reason_code")]
        public string? ReasonCode { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("reason")]
        public string? Reason { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("status_text")]
        public string? StatusText { get; set; }
    }

    [HttpPost("offline-bopis-request")]
    [Authorize(Roles = "branch_manager,admin")]
    public async Task<IActionResult> CreateOfflineBopis([FromBody] CreateOfflineBopisRequest request)
    {
        if (GetUserId() == null) return Unauthorized();
        var result = await Mediator.Send(new CreateOfflineBopisOrderCommand { BrandManagerId = GetUserId()!.Value, Request = request });
        return Ok(ApiResponse<OrderResponse>.SuccessResponse(result, "Offline BOPIS request created successfully", 201));
    }

    /// <summary>
    /// Staff confirms the customer has collected a BOPIS order at the counter.
    /// Transitions ready_for_pickup → picked_up and records the balance payment.
    /// </summary>
    [HttpPost("{id:guid}/mark-picked-up")]
    [Authorize(Roles = "store_staff,fulfillment_staff,branch_manager,admin")]
    public async Task<IActionResult> MarkPickedUp(Guid id, [FromBody] MarkOrderPickedUpRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var result = await Mediator.Send(new MarkOrderPickedUpCommand
        {
            OrderId = id,
            StaffUserId = userId.Value,
            Request = request
        });
        return Ok(ApiResponse<OrderResponse>.SuccessResponse(result, "Order marked as picked up"));
    }
}
