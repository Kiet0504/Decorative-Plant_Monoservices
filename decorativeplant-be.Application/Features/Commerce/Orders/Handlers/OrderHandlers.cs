using System.Globalization;
using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using decorativeplant_be.Application.Common.DTOs.Commerce;
using decorativeplant_be.Application.Common.DTOs.Common;
using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Commerce.Orders.Commands;
using decorativeplant_be.Application.Features.Commerce.Orders.Queries;
using decorativeplant_be.Application.Features.Commerce.Vouchers;
using decorativeplant_be.Application.Services;
using decorativeplant_be.Domain.Entities;
using decorativeplant_be.Application.Features.Commerce.Orders;

namespace decorativeplant_be.Application.Features.Commerce.Orders.Handlers;


public class CreateOrderHandler : IRequestHandler<CreateOrderCommand, List<OrderResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<CreateOrderHandler> _logger;
    private readonly IShippingService _shippingService;
    private readonly IBranchAllocationService _allocationService;
    private readonly IStockService _stockService;

    public CreateOrderHandler(
        IApplicationDbContext context,
        ILogger<CreateOrderHandler> logger,
        IShippingService shippingService,
        IBranchAllocationService allocationService,
        IStockService stockService)
    {
        _context = context;
        _logger = logger;
        _shippingService = shippingService;
        _allocationService = allocationService;
        _stockService = stockService;
    }

    public async Task<List<OrderResponse>> Handle(CreateOrderCommand cmd, CancellationToken ct)
    {
        var req = cmd.Request;

        // Use execution strategy to support NpgsqlRetryingExecutionStrategy with transactions
        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
        using var transaction = await _context.Database.BeginTransactionAsync(ct);
        try
        {
            // 1. Use Branch Allocation Service to resolve optimal branch for each item
            //    Chain Store model: customer sends listingId (primary), BE finds best branch with stock
            var requestedItems = req.Items.Select(i => (i.ListingId, i.Quantity)).ToList();
            var allocations = await _allocationService.AllocateAsync(requestedItems, ct);

            // Map allocations to enriched items (compatible with existing order creation logic)
            var enrichedItems = allocations.Select(a => (
                reqItem: new CreateOrderItemRequest { ListingId = a.Listing.Id, Quantity = a.AllocatedQuantity },
                listing: a.Listing,
                unitPrice: a.UnitPrice,
                title: a.Title,
                image: a.Image
            )).ToList();

            // 2. Build all OrderItems (no branch grouping — 1 unified order)
            var orderItems = new List<OrderItem>();
            decimal cartSubtotal = 0;

            foreach (var e in enrichedItems)
            {
                if (!decimal.TryParse(e.unitPrice, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsedPrice))
                    throw new BadRequestException($"Invalid price for '{e.title ?? e.reqItem.ListingId.ToString()}'.");
                var itemSubtotal = parsedPrice * e.reqItem.Quantity;
                cartSubtotal += itemSubtotal;

                // Reserve stock with pessimistic locking via StockService
                var productName = e.title ?? e.reqItem.ListingId.ToString();
                await _stockService.ReserveStockAsync(e.listing.Id, e.listing.BranchId, e.reqItem.Quantity, productName, ct);

                orderItems.Add(new OrderItem
                {
                    Id = Guid.NewGuid(),
                    ListingId = e.reqItem.ListingId,
                    BatchId = e.listing.BatchId,
                    BranchId = e.listing.BranchId,
                    Quantity = e.reqItem.Quantity,
                    Pricing = JsonDocument.Parse(JsonSerializer.Serialize(new { unit_price = e.unitPrice, subtotal = itemSubtotal.ToString("0", CultureInfo.InvariantCulture) })),
                    Snapshots = JsonDocument.Parse(JsonSerializer.Serialize(new { title_snapshot = e.title, image_snapshot = e.image }))
                });
            }

            // 3. Resolve voucher (applied once to the whole order)
            decimal totalDiscount = 0;
            Voucher? voucher = null;
            Guid? appliedVoucherId = null;

            if (!string.IsNullOrEmpty(req.VoucherCode))
            {
                var pending = await _context.Vouchers.FirstOrDefaultAsync(v => v.Code == req.VoucherCode && v.IsActive, ct);
                if (pending != null)
                {
                    // Lock the voucher row inside this transaction, then re-fetch with AsNoTracking
                    // discarded so the tracked entity reflects the locked row's latest state. Parallel
                    // checkouts now serialize on this row and can't both pass the usage_limit check.
                    await _context.AcquireVoucherLockAsync(pending.Id, ct);
                    voucher = await _context.Vouchers.FirstOrDefaultAsync(v => v.Id == pending.Id, ct);
                }
                if (voucher != null)
                {

                    // For chain store model, voucher applies to the whole order
                    // (branch-specific vouchers are still checked for eligibility)
                    if (voucher.BranchId.HasValue)
                    {
                        var branchIdsInCart = enrichedItems.Select(e => e.listing.BranchId).Distinct().ToList();
                        if (!branchIdsInCart.Contains(voucher.BranchId))
                        {
                            _logger.LogWarning("Voucher {Code} belongs to branch {BranchId} but cart has no items from that branch.", req.VoucherCode, voucher.BranchId);
                            voucher = null;
                        }
                    }
                }

                if (voucher != null)
                {
                    int usageLimit = int.MaxValue;
                    int usedCount = 0;
                    decimal minOrder = 0;

                    if (voucher.Rules != null)
                    {
                        var rulesRoot = voucher.Rules.RootElement;
                        usageLimit = rulesRoot.TryGetProperty("usage_limit", out var ul) && ul.ValueKind == JsonValueKind.Number ? ul.GetInt32() : int.MaxValue;
                        usedCount = rulesRoot.TryGetProperty("used_count", out var uc) && uc.ValueKind == JsonValueKind.Number ? uc.GetInt32() : 0;
                        minOrder = rulesRoot.TryGetProperty("min_order_amount", out var mo) && mo.ValueKind == JsonValueKind.String ? decimal.Parse(mo.GetString() ?? "0", CultureInfo.InvariantCulture) : 0;
                    }

                    decimal eligibleTotal = voucher.BranchId.HasValue
                        ? enrichedItems.Where(e => e.listing.BranchId == voucher.BranchId).Sum(e => decimal.Parse(e.unitPrice, CultureInfo.InvariantCulture) * e.reqItem.Quantity)
                        : cartSubtotal;

                    if (usedCount < usageLimit && eligibleTotal >= minOrder)
                    {
                        if (voucher.Info != null)
                        {
                            var infoRoot = voucher.Info.RootElement;
                            var type = infoRoot.TryGetProperty("discount_type", out var dt) ? dt.GetString() : null;
                            var valStr = infoRoot.TryGetProperty("discount_value", out var dv) ? dv.GetString() ?? "0" : "0";
                            var val = decimal.Parse(valStr, CultureInfo.InvariantCulture);

                            if (type == "percentage") totalDiscount = eligibleTotal * (val / 100);
                            else if (type == "fixed") totalDiscount = val;
                        }

                        if (totalDiscount > eligibleTotal) totalDiscount = eligibleTotal;

                        if (voucher.Rules != null)
                        {
                            var rules = new Dictionary<string, object?>();
                            foreach (var p in voucher.Rules.RootElement.EnumerateObject())
                                rules[p.Name] = p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() :
                                    (p.Value.ValueKind == JsonValueKind.Number ? p.Value.GetDouble() : p.Value.GetRawText());
                            rules["used_count"] = usedCount + 1;
                            voucher.Rules = JsonDocument.Parse(JsonSerializer.Serialize(rules));
                        }
                        appliedVoucherId = voucher.Id;
                    }
                    else
                    {
                        totalDiscount = 0;
                    }
                }
            }

            // 4. Calculate shipping fee (1 fee for the whole order — all stores are in HCM)
            decimal shippingFee = 30000; // sensible default
            if (req.DeliveryAddress != null)
            {
                try
                {
                    var feeResult = await _shippingService.CalculateFeeAsync(new ShippingFeeRequest
                    {
                        FromDistrictId = _shippingService.DefaultFromDistrictId,
                        FromWardCode = _shippingService.DefaultFromWardCode,
                        ToDistrictId = req.DeliveryAddress.DistrictId,
                        ToWardCode = req.DeliveryAddress.WardCode,
                        Weight = Math.Max(orderItems.Sum(oi => oi.Quantity) * 500, 500), // ~500g per item, min 500g
                        InsuranceValue = (int)cartSubtotal,
                        ServiceTypeId = 2
                    });
                    if (feeResult.Success && feeResult.Total > 0) shippingFee = feeResult.Total;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "GHN fee calc failed, using default 30000");
                }
            }
            else if (req.ShippingFee > 0)
            {
                shippingFee = req.ShippingFee;
            }

            shippingFee = Math.Round(shippingFee, 0);
            totalDiscount = Math.Round(totalDiscount, 0);

            decimal orderTotal = cartSubtotal + shippingFee - totalDiscount;
            if (orderTotal < 0) orderTotal = 0;

            // 5. Create 1 unified OrderHeader (BranchId = null — system-level order)
            var orderCode = $"ORD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpper()}";

            var order = new OrderHeader
            {
                Id = Guid.NewGuid(),
                OrderCode = orderCode,
                UserId = cmd.UserId,
                VoucherId = appliedVoucherId,
                TypeInfo = JsonDocument.Parse(JsonSerializer.Serialize(new { order_type = req.OrderType, fulfillment_method = req.FulfillmentMethod })),
                Financials = JsonDocument.Parse(JsonSerializer.Serialize(new { subtotal = cartSubtotal.ToString("0", CultureInfo.InvariantCulture), shipping = shippingFee.ToString("0", CultureInfo.InvariantCulture), discount = totalDiscount.ToString("0", CultureInfo.InvariantCulture), tax = "0", total = orderTotal.ToString("0", CultureInfo.InvariantCulture) })),
                Status = "pending",
                Notes = !string.IsNullOrEmpty(req.CustomerNote) ? JsonDocument.Parse(JsonSerializer.Serialize(new { customer_note = req.CustomerNote })) : null,
                DeliveryAddress = req.DeliveryAddress != null ? JsonDocument.Parse(JsonSerializer.Serialize(new { recipient_name = req.DeliveryAddress.RecipientName, phone = req.DeliveryAddress.Phone, address_line_1 = req.DeliveryAddress.AddressLine1, city = req.DeliveryAddress.City, district_id = req.DeliveryAddress.DistrictId, ward_code = req.DeliveryAddress.WardCode })) : null,
                CreatedAt = DateTime.UtcNow,
                OrderItems = orderItems
            };

            // Seed status history
            OrderStatusMachine.AppendHistory(order, from: null, to: OrderStatusMachine.Pending,
                changedBy: cmd.UserId, reason: "Order created", source: "CreateOrder");

            _context.OrderHeaders.Add(order);
            _logger.LogInformation("Created unified Order {OrderCode} with {ItemCount} items from {BranchCount} branch(es)",
                orderCode, orderItems.Count,
                enrichedItems.Select(e => e.listing.BranchId).Distinct().Count());

            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return new List<OrderResponse> { MapToResponse(order) };
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
        }); // end strategy.ExecuteAsync
    }

    internal static OrderResponse MapToResponse(OrderHeader o)
    {
        var response = new OrderResponse
        {
            Id = o.Id, OrderCode = o.OrderCode, UserId = o.UserId,
            Status = o.Status ?? "pending", CreatedAt = o.CreatedAt, ConfirmedAt = o.ConfirmedAt
        };

        if (o.TypeInfo != null)
        {
            var root = o.TypeInfo.RootElement;
            response.OrderType = root.TryGetProperty("order_type", out var ot) ? ot.GetString() : null;
            response.FulfillmentMethod = root.TryGetProperty("fulfillment_method", out var fm) ? fm.GetString() : null;
        }
        if (o.Financials != null)
        {
            var root = o.Financials.RootElement;
            response.Financials = new OrderFinancialsDto
            {
                Subtotal = root.TryGetProperty("subtotal", out var s) ? s.GetString() ?? "0" : "0",
                Shipping = root.TryGetProperty("shipping", out var sh) ? sh.GetString() ?? "0" : "0",
                Discount = root.TryGetProperty("discount", out var d) ? d.GetString() ?? "0" : "0",
                Tax = root.TryGetProperty("tax", out var t) ? t.GetString() ?? "0" : "0",
                Total = root.TryGetProperty("total", out var tl) ? tl.GetString() ?? "0" : "0"
            };
        }
        if (o.DeliveryAddress != null)
        {
            var root = o.DeliveryAddress.RootElement;
            response.DeliveryAddress = new DeliveryAddressDto
            {
                RecipientName = root.TryGetProperty("recipient_name", out var rn) ? rn.GetString() ?? "" : "",
                Phone = root.TryGetProperty("phone", out var ph) ? ph.GetString() ?? "" : "",
                AddressLine1 = root.TryGetProperty("address_line_1", out var a1) ? a1.GetString() ?? "" : "",
                City = root.TryGetProperty("city", out var c) ? c.GetString() : null
            };
        }
        if (o.Notes != null)
        {
            var root = o.Notes.RootElement;
            response.CustomerNote = root.TryGetProperty("customer_note", out var cn) ? cn.GetString() : null;
            response.PaymentStatus = root.TryGetProperty("payment_status", out var ps) ? ps.GetString() : null;
            response.TrackingCode = root.TryGetProperty("tracking_code", out var tc) ? tc.GetString() : null;
            response.CarrierName = root.TryGetProperty("carrier_name", out var cn2) ? cn2.GetString() : null;
        }
        if (o.OrderItems != null)
        {
            response.Items = o.OrderItems.Select(oi =>
            {
                var item = new OrderItemResponse { Id = oi.Id, ListingId = oi.ListingId, StockId = oi.StockId, BranchId = oi.BranchId, Quantity = oi.Quantity };
                if (oi.Pricing != null)
                {
                    var pr = oi.Pricing.RootElement;
                    item.UnitPrice = pr.TryGetProperty("unit_price", out var up) ? up.GetString() : null;
                    item.Subtotal = pr.TryGetProperty("subtotal", out var st) ? st.GetString() : null;
                }
                if (oi.Snapshots != null)
                {
                    var sn = oi.Snapshots.RootElement;
                    item.TitleSnapshot = sn.TryGetProperty("title_snapshot", out var ts) ? ts.GetString() : null;
                    item.ImageSnapshot = sn.TryGetProperty("image_snapshot", out var ims) ? ims.GetString() : null;
                }
                return item;
            }).ToList();
        }
        return response;
    }
}

public class UpdateOrderStatusHandler : IRequestHandler<UpdateOrderStatusCommand, OrderResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IStockService _stockService;
    private readonly IShippingService _shippingService;
    private readonly ILogger<UpdateOrderStatusHandler> _logger;

    public UpdateOrderStatusHandler(IApplicationDbContext context, IStockService stockService, IShippingService shippingService, ILogger<UpdateOrderStatusHandler> logger)
    {
        _context = context;
        _stockService = stockService;
        _shippingService = shippingService;
        _logger = logger;
    }

    public async Task<OrderResponse> Handle(UpdateOrderStatusCommand cmd, CancellationToken ct)
    {
        var order = await _context.OrderHeaders.Include(o => o.OrderItems).FirstOrDefaultAsync(o => o.Id == cmd.Id, ct)
            ?? throw new NotFoundException($"Order {cmd.Id} not found.");

        var normalizedStatus = cmd.Request.Status?.ToLowerInvariant() ?? "";

        // Block staff from setting "completed" — only the customer may do that via POST /confirm-receipt
        if (normalizedStatus == OrderStatusMachine.Completed)
            throw new BadRequestException("Only the customer can mark an order as completed (via Confirm Receipt).");

        // Validate + append audit entry
        OrderStatusMachine.Apply(order, normalizedStatus, cmd.ActorUserId,
            reason: cmd.Request.RejectionReason ?? cmd.Request.InternalNote,
            source: "StaffUpdate");

        // Merge side-note fields (internal_note, tracking_code, carrier_name) while preserving history.
        var notesDict = MergeNotes(order,
            internalNote: cmd.Request.InternalNote,
            rejectionReason: cmd.Request.RejectionReason,
            trackingCode: cmd.Request.TrackingCode,
            carrierName: cmd.Request.CarrierName);
        order.Notes = JsonDocument.Parse(JsonSerializer.Serialize(notesDict));

        // If order is confirmed, try to create GHN shipments
        if (normalizedStatus == OrderStatusMachine.Confirmed)
        {
            await GhnOrderHelper.TryCreateGhnOrderAsync(order, _shippingService, _logger);
        }

        await _context.SaveChangesAsync(ct);
        return CreateOrderHandler.MapToResponse(order);
    }

    private static Dictionary<string, object?> MergeNotes(
        OrderHeader order,
        string? internalNote, string? rejectionReason,
        string? trackingCode, string? carrierName)
    {
        var dict = new Dictionary<string, object?>();
        if (order.Notes != null)
        {
            foreach (var p in order.Notes.RootElement.EnumerateObject())
            {
                if (p.Value.ValueKind == JsonValueKind.String) dict[p.Name] = p.Value.GetString();
                else dict[p.Name] = JsonSerializer.Deserialize<object?>(p.Value.GetRawText());
            }
        }
        if (!string.IsNullOrEmpty(internalNote))     dict["internal_note"]    = internalNote;
        if (!string.IsNullOrEmpty(rejectionReason))  dict["rejection_reason"] = rejectionReason;
        if (!string.IsNullOrEmpty(trackingCode))     dict["tracking_code"]    = trackingCode;
        if (!string.IsNullOrEmpty(carrierName))      dict["carrier_name"]     = carrierName;
        return dict;
    }
}

public class CancelOrderHandler : IRequestHandler<CancelOrderCommand, OrderResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IStockService _stockService;
    private readonly IPayOSService _payOS;
    private readonly ILogger<CancelOrderHandler> _logger;

    public CancelOrderHandler(
        IApplicationDbContext context,
        IStockService stockService,
        IPayOSService payOS,
        ILogger<CancelOrderHandler> logger)
    {
        _context = context;
        _stockService = stockService;
        _payOS = payOS;
        _logger = logger;
    }

    public async Task<OrderResponse> Handle(CancelOrderCommand cmd, CancellationToken ct)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
        using var transaction = await _context.Database.BeginTransactionAsync(ct);
        try
        {
        var order = await _context.OrderHeaders.Include(o => o.OrderItems).FirstOrDefaultAsync(o => o.Id == cmd.Id, ct)
            ?? throw new NotFoundException($"Order {cmd.Id} not found.");

        // Validate ownership — users can only cancel their own orders
        if (cmd.UserId.HasValue && order.UserId != cmd.UserId)
            throw new BadRequestException("You can only cancel your own orders.");

        // Paid orders past "pending" must go through the return/refund flow so money movement
        // is explicit. OrderStatusMachine alone allows step<2 to cancel (i.e. confirmed → cancelled),
        // but once money has settled we can't silently cancel — refuse here.
        if (!string.Equals(order.Status, OrderStatusMachine.Pending, StringComparison.OrdinalIgnoreCase))
        {
            var orderPayments = await _context.PaymentTransactions
                .Where(p => p.OrderId == order.Id && p.Details != null)
                .ToListAsync(ct);
            var hasPaidPayment = orderPayments.Any(p =>
            {
                var root = p.Details!.RootElement;
                var status = root.TryGetProperty("status", out var st) ? st.GetString() : null;
                var type = root.TryGetProperty("type", out var tp) ? tp.GetString() : null;
                return string.Equals(status, "paid", StringComparison.OrdinalIgnoreCase)
                       && !string.Equals(type, "refund", StringComparison.OrdinalIgnoreCase);
            });
            if (hasPaidPayment)
            {
                throw new BadRequestException(
                    "This order has already been paid. Please submit a return request instead of cancelling.");
            }
        }

        var isBopis = OrderStatusMachine.IsBopis(order.Status);

        // For BOPIS: if any linked StockTransfer has already shipped, stock is in transit
        // and cancellation would strand it. Require admin reversal via the inventory flow instead.
        if (isBopis)
        {
            var transfers = await _context.StockTransfers
                .Where(t => t.LogisticsInfo != null)
                .ToListAsync(ct);
            var linked = transfers.Where(t =>
                t.LogisticsInfo!.RootElement.TryGetProperty("order_id", out var oid)
                && oid.TryGetGuid(out var g) && g == order.Id).ToList();
            if (linked.Any(t => string.Equals(t.Status, "shipped", StringComparison.OrdinalIgnoreCase)
                             || string.Equals(t.Status, "received", StringComparison.OrdinalIgnoreCase)))
            {
                throw new BadRequestException(
                    "Cannot cancel: stock transfer already shipped. Reverse the transfer from the inventory module first.");
            }
            // Cancel the open transfer requests so stock isn't reserved downstream.
            foreach (var t in linked)
            {
                if (!string.Equals(t.Status, "cancelled", StringComparison.OrdinalIgnoreCase))
                    t.Status = "cancelled";
            }
        }

        OrderStatusMachine.Apply(order, OrderStatusMachine.Cancelled, cmd.UserId,
            reason: cmd.Request.CancellationReason, source: "CustomerCancel");

        var notes = new Dictionary<string, object?>();
        if (order.Notes != null)
            foreach (var p in order.Notes.RootElement.EnumerateObject())
            {
                if (p.Value.ValueKind == JsonValueKind.String) notes[p.Name] = p.Value.GetString();
                else notes[p.Name] = JsonSerializer.Deserialize<object?>(p.Value.GetRawText());
            }
        notes["cancellation_reason"] = cmd.Request.CancellationReason;
        order.Notes = JsonDocument.Parse(JsonSerializer.Serialize(notes));

        if (isBopis)
        {
            // BOPIS did not reserve stock at creation (sourcing happens at StockTransfer-approve),
            // so there is nothing to RestoreOrderStock. Instead, record a deposit refund for finance.
            decimal depositPaid = 0;
            if (order.Financials != null
                && order.Financials.RootElement.TryGetProperty("amount_paid", out var apEl)
                && decimal.TryParse(apEl.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var ap))
            {
                depositPaid = ap;
            }
            if (depositPaid > 0)
            {
                _context.PaymentTransactions.Add(new PaymentTransaction
                {
                    Id = Guid.NewGuid(),
                    OrderId = order.Id,
                    TransactionCode = $"REF-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpper()}",
                    Details = JsonDocument.Parse(JsonSerializer.Serialize(new
                    {
                        provider = "offline",
                        type = "refund",
                        amount = depositPaid.ToString("0", CultureInfo.InvariantCulture),
                        status = "pending", // actual refund handled out-of-band by finance
                        reason = cmd.Request.CancellationReason,
                        refunded_by = cmd.UserId
                    })),
                    CreatedAt = DateTime.UtcNow
                });
            }
        }
        else
        {
            // Standard delivery flow: restore reserved stock with pessimistic locking.
            if (order.OrderItems != null)
                await _stockService.RestoreOrderStockAsync(order.OrderItems, ct);
        }

        // Roll back voucher usage so the same user can re-apply the code.
        if (order.VoucherId.HasValue)
            await VoucherUsageHelper.RollbackUsageAsync(_context, order.VoucherId.Value, ct);

        // Payment reconciliation:
        //   - Unpaid PayOS link     → cancel the link so user can't still pay and land on a cancelled order.
        //   - Already-paid PayOS    → record a pending refund PaymentTransaction for finance (PayOS has no
        //                             programmatic refund; actual money movement is out-of-band).
        await TryHandlePayOSOnCancelAsync(order, cmd.Request.CancellationReason, cmd.UserId, ct);

        await _context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return CreateOrderHandler.MapToResponse(order);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
        }); // end strategy.ExecuteAsync
    }

    /// <summary>
    /// Cancels the PayOS payment link if still unpaid, otherwise records a pending
    /// offline refund PaymentTransaction so finance can reconcile. Swallows and logs
    /// errors — gateway hiccups must not block order cancellation.
    /// </summary>
    private async Task TryHandlePayOSOnCancelAsync(
        OrderHeader order,
        string? reason,
        Guid? actorId,
        CancellationToken ct)
    {
        var payments = await _context.PaymentTransactions
            .Where(p => p.OrderId == order.Id && p.Details != null)
            .ToListAsync(ct);

        foreach (var p in payments)
        {
            if (p.Details == null) continue;
            var root = p.Details.RootElement;
            var provider = root.TryGetProperty("provider", out var pv) ? pv.GetString() : null;
            if (!string.Equals(provider, "payos", StringComparison.OrdinalIgnoreCase)) continue;

            var status = root.TryGetProperty("status", out var st) ? st.GetString() : null;
            var amount = root.TryGetProperty("amount", out var am) ? am.GetString() : null;

            if (string.Equals(status, "paid", StringComparison.OrdinalIgnoreCase))
            {
                _context.PaymentTransactions.Add(new PaymentTransaction
                {
                    Id = Guid.NewGuid(),
                    OrderId = order.Id,
                    TransactionCode = $"REF-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpper()}",
                    Details = JsonDocument.Parse(JsonSerializer.Serialize(new
                    {
                        provider = "payos",
                        type = "refund",
                        amount,
                        status = "pending",
                        reason,
                        refunded_by = actorId,
                        source_payment_id = p.Id,
                    })),
                    CreatedAt = DateTime.UtcNow,
                });
                continue;
            }

            // Unpaid link — cancel on PayOS so the user can't complete payment after cancellation.
            if (!root.TryGetProperty("payos_order_code", out var pocEl)) continue;
            long payosOrderCode = pocEl.ValueKind switch
            {
                JsonValueKind.Number => pocEl.GetInt64(),
                JsonValueKind.String when long.TryParse(pocEl.GetString(), out var parsed) => parsed,
                _ => 0,
            };
            if (payosOrderCode <= 0) continue;

            try
            {
                await _payOS.CancelPaymentLinkAsync(payosOrderCode, reason, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "PayOS link cancel failed for order {OrderCode} (payos={PayOS}). Ignoring.", order.OrderCode, payosOrderCode);
            }
        }
    }
}

public class ConfirmReceiptHandler : IRequestHandler<ConfirmReceiptCommand, OrderResponse>
{
    private readonly IApplicationDbContext _context;
    public ConfirmReceiptHandler(IApplicationDbContext context) => _context = context;

    public async Task<OrderResponse> Handle(ConfirmReceiptCommand cmd, CancellationToken ct)
    {
        var order = await _context.OrderHeaders.Include(o => o.OrderItems).FirstOrDefaultAsync(o => o.Id == cmd.OrderId, ct)
            ?? throw new NotFoundException($"Order {cmd.OrderId} not found.");

        if (order.UserId != cmd.UserId)
            throw new BadRequestException("You can only confirm receipt for your own orders.");

        OrderStatusMachine.Apply(order, OrderStatusMachine.Completed, cmd.UserId,
            reason: "Customer confirmed receipt", source: "ConfirmReceipt");

        var notesDict = new Dictionary<string, object?>();
        if (order.Notes != null)
            foreach (var p in order.Notes.RootElement.EnumerateObject())
            {
                if (p.Value.ValueKind == JsonValueKind.String) notesDict[p.Name] = p.Value.GetString();
                else notesDict[p.Name] = JsonSerializer.Deserialize<object?>(p.Value.GetRawText());
            }
        notesDict["completed_at"] = DateTime.UtcNow.ToString("o");

        order.Notes = JsonDocument.Parse(JsonSerializer.Serialize(notesDict));

        await _context.SaveChangesAsync(ct);
        return CreateOrderHandler.MapToResponse(order);
    }
}

public class GetOrdersHandler : IRequestHandler<GetOrdersQuery, PagedResult<OrderResponse>>
{
    private readonly IApplicationDbContext _context;
    public GetOrdersHandler(IApplicationDbContext context) => _context = context;

    public async Task<PagedResult<OrderResponse>> Handle(GetOrdersQuery query, CancellationToken ct)
    {
        var q = _context.OrderHeaders.Include(o => o.OrderItems).AsQueryable();
        if (query.UserId.HasValue) q = q.Where(o => o.UserId == query.UserId);
        // Chain Store: filter by branch at OrderItem level
        if (query.BranchId.HasValue) q = q.Where(o => o.OrderItems.Any(oi => oi.BranchId == query.BranchId));
        if (!string.IsNullOrEmpty(query.Status)) q = q.Where(o => o.Status == query.Status);

        var total = await q.CountAsync(ct);
        
        var orders = await q.OrderByDescending(o => o.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);
            
        return new PagedResult<OrderResponse>
        {
            Items = orders.Select(o => CreateOrderHandler.MapToResponse(o)).ToList(),
            TotalCount = total,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }
}

public class GetOrderByIdHandler : IRequestHandler<GetOrderByIdQuery, OrderResponse?>
{
    private readonly IApplicationDbContext _context;
    public GetOrderByIdHandler(IApplicationDbContext context) => _context = context;

    public async Task<OrderResponse?> Handle(GetOrderByIdQuery query, CancellationToken ct)
    {
        var order = await _context.OrderHeaders.Include(o => o.OrderItems).FirstOrDefaultAsync(o => o.Id == query.Id, ct);
        if (order == null) return null;

        // Non-admin users can only view their own orders
        if (query.UserId.HasValue && order.UserId != query.UserId)
            throw new BadRequestException("You do not have permission to view this order.");

        return CreateOrderHandler.MapToResponse(order);
    }
}

public class CreateOfflineBopisOrderHandler : IRequestHandler<CreateOfflineBopisOrderCommand, OrderResponse>
{
    private readonly IApplicationDbContext _context;

    public CreateOfflineBopisOrderHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<OrderResponse> Handle(CreateOfflineBopisOrderCommand cmd, CancellationToken ct)
    {
        var req = cmd.Request;

        decimal cartSubtotal = req.Items.Sum(i => i.Quantity * i.UnitPrice);
        if (cartSubtotal <= 0)
            throw new BadRequestException("Order subtotal must be greater than 0");

        decimal minDeposit = cartSubtotal * 0.3m;
        if (req.DepositAmount < minDeposit)
            throw new BadRequestException($"Deposit amount must be at least 30% ({minDeposit.ToString("N0", CultureInfo.InvariantCulture)} VND)");

        decimal remainingBalance = Math.Max(0, cartSubtotal - req.DepositAmount);

        var orderCode = $"BOPIS-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpper()}";
        var orderId = Guid.NewGuid();

        var orderItems = new List<OrderItem>();
        foreach (var itemReq in req.Items)
        {
            // BranchId stays null until ApproveStockTransferCommandHandler picks a source branch
            // and back-fills it to attribute revenue.
            orderItems.Add(new OrderItem
            {
                Id = Guid.NewGuid(),
                OrderId = orderId,
                ListingId = itemReq.ListingId,
                Quantity = itemReq.Quantity,
                Pricing = JsonDocument.Parse(JsonSerializer.Serialize(new {
                    unit_price = itemReq.UnitPrice.ToString("0", CultureInfo.InvariantCulture),
                    subtotal = (itemReq.Quantity * itemReq.UnitPrice).ToString("0", CultureInfo.InvariantCulture)
                }))
            });

            _context.StockTransfers.Add(new StockTransfer
            {
                Id = Guid.NewGuid(),
                TransferCode = $"REQ-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}",
                ToBranchId = req.PickupBranchId,
                Quantity = itemReq.Quantity,
                Status = "requested",
                LogisticsInfo = JsonDocument.Parse(JsonSerializer.Serialize(new
                {
                    order_id = orderId,
                    order_code = orderCode,
                    listing_id = itemReq.ListingId
                })),
                CreatedAt = DateTime.UtcNow
            });
        }

        var order = new OrderHeader
        {
            Id = orderId,
            OrderCode = orderCode,
            UserId = req.CustomerUserId,
            TypeInfo = JsonDocument.Parse(JsonSerializer.Serialize(new {
                order_type = "offline",
                fulfillment_method = "bopis_transfer",
                created_by = cmd.BrandManagerId
            })),
            Financials = JsonDocument.Parse(JsonSerializer.Serialize(new {
                subtotal = cartSubtotal.ToString("0", CultureInfo.InvariantCulture),
                discount = "0",
                tax = "0",
                shipping = "0",
                total = cartSubtotal.ToString("0", CultureInfo.InvariantCulture),
                amount_paid = req.DepositAmount.ToString("0", CultureInfo.InvariantCulture),
                remaining_balance = remainingBalance.ToString("0", CultureInfo.InvariantCulture)
            })),
            Status = OrderStatusMachine.DepositPaid,
            DeliveryAddress = JsonDocument.Parse(JsonSerializer.Serialize(new {
                pickup_branch_id = req.PickupBranchId,
                recipient_name = req.CustomerName,
                phone = req.CustomerPhone
            })),
            Notes = JsonDocument.Parse(JsonSerializer.Serialize(new {
                payment_method = req.PaymentMethod,
                sourcing_required = true
            })),
            CreatedAt = DateTime.UtcNow,
            OrderItems = orderItems
        };

        OrderStatusMachine.AppendHistory(order,
            from: null, to: OrderStatusMachine.DepositPaid,
            changedBy: cmd.BrandManagerId,
            reason: "Offline BOPIS order created",
            source: "BrandManagerCreate");

        _context.OrderHeaders.Add(order);

        // Record deposit as a completed PaymentTransaction so finance/reporting stays in parity
        // with online orders. Remaining balance is collected on pickup (MarkPickedUp handler).
        _context.PaymentTransactions.Add(new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            TransactionCode = $"DEP-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpper()}",
            Details = JsonDocument.Parse(JsonSerializer.Serialize(new
            {
                provider = "offline",
                method = req.PaymentMethod,
                type = "deposit",
                amount = req.DepositAmount.ToString("0", CultureInfo.InvariantCulture),
                status = "completed",
                collected_by = cmd.BrandManagerId
            })),
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync(ct);

        return CreateOrderHandler.MapToResponse(order);
    }
}

/// <summary>
/// Store staff marks a BOPIS order as picked up after collecting the remaining balance
/// at the counter. Transitions ready_for_pickup → picked_up (terminal) and finalizes stock
/// deduction for revenue recognition.
/// </summary>
public class MarkOrderPickedUpHandler : IRequestHandler<MarkOrderPickedUpCommand, OrderResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IStockService _stockService;

    public MarkOrderPickedUpHandler(IApplicationDbContext context, IStockService stockService)
    {
        _context = context;
        _stockService = stockService;
    }

    public async Task<OrderResponse> Handle(MarkOrderPickedUpCommand cmd, CancellationToken ct)
    {
        var order = await _context.OrderHeaders.Include(o => o.OrderItems)
            .FirstOrDefaultAsync(o => o.Id == cmd.OrderId, ct)
            ?? throw new NotFoundException($"Order {cmd.OrderId} not found.");

        if (!OrderStatusMachine.IsBopis(order.Status))
            throw new BadRequestException("Only BOPIS orders can be marked as picked up.");

        if (order.Status != OrderStatusMachine.ReadyForPickup)
            throw new BadRequestException(
                $"Order must be in '{OrderStatusMachine.ReadyForPickup}' state to mark as picked up (current: '{order.Status}').");

        // Validate that the collected balance matches the outstanding amount.
        decimal expected = 0;
        if (order.Financials != null
            && order.Financials.RootElement.TryGetProperty("remaining_balance", out var rbEl)
            && decimal.TryParse(rbEl.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var rb))
        {
            expected = rb;
        }
        if (cmd.Request.BalanceCollected != expected)
            throw new BadRequestException(
                $"Collected amount {cmd.Request.BalanceCollected} does not match remaining balance {expected}.");

        OrderStatusMachine.Apply(order, OrderStatusMachine.PickedUp, cmd.StaffUserId,
            reason: "Customer picked up order", source: "StaffMarkPickedUp");

        // Zero out remaining_balance and bump amount_paid to the order total.
        if (order.Financials != null)
        {
            var fin = new Dictionary<string, object?>();
            foreach (var p in order.Financials.RootElement.EnumerateObject())
            {
                fin[p.Name] = p.Value.ValueKind == JsonValueKind.String
                    ? p.Value.GetString()
                    : JsonSerializer.Deserialize<object?>(p.Value.GetRawText());
            }
            if (fin.TryGetValue("total", out var totalObj)
                && totalObj is string totalStr
                && decimal.TryParse(totalStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var total))
            {
                fin["amount_paid"] = total.ToString("0", CultureInfo.InvariantCulture);
            }
            fin["remaining_balance"] = "0";
            order.Financials = JsonDocument.Parse(JsonSerializer.Serialize(fin));
        }

        // Finalize stock: deduct reserved quantities so available/reserved line up.
        // Safe for BOPIS because ApproveStockTransfer set OrderItem.BranchId to the source branch
        // and ShipStockTransfer already decremented BatchStock. RestoreOrderStock path is NOT taken
        // here — we just record the sale.
        if (order.OrderItems != null && order.OrderItems.Count > 0)
        {
            await _stockService.DeductOrderStockAsync(order.OrderItems, ct);
        }

        // Record the balance collection as a PaymentTransaction (skip if prepaid in full).
        if (cmd.Request.BalanceCollected > 0)
        {
            _context.PaymentTransactions.Add(new PaymentTransaction
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                TransactionCode = $"BAL-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpper()}",
                Details = JsonDocument.Parse(JsonSerializer.Serialize(new
                {
                    provider = "offline",
                    method = cmd.Request.PaymentMethod,
                    type = "balance",
                    amount = cmd.Request.BalanceCollected.ToString("0", CultureInfo.InvariantCulture),
                    status = "completed",
                    collected_by = cmd.StaffUserId
                })),
                CreatedAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync(ct);
        return CreateOrderHandler.MapToResponse(order);
    }
}
