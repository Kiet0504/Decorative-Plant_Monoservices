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
using decorativeplant_be.Application.Services;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Application.Features.Commerce.Orders.Handlers;

public class CreateOrderHandler : IRequestHandler<CreateOrderCommand, List<OrderResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<CreateOrderHandler> _logger;
    private readonly IShippingService _shippingService;
    private readonly IBranchAllocationService _allocationService;

    public CreateOrderHandler(
        IApplicationDbContext context, 
        ILogger<CreateOrderHandler> logger, 
        IShippingService shippingService,
        IBranchAllocationService allocationService)
    {
        _context = context;
        _logger = logger;
        _shippingService = shippingService;
        _allocationService = allocationService;
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
                var itemSubtotal = decimal.Parse(e.unitPrice) * e.reqItem.Quantity;
                cartSubtotal += itemSubtotal;

                // Reserve stock
                if (e.listing.BatchId.HasValue)
                {
                    var stock = await _context.BatchStocks
                        .FirstOrDefaultAsync(s => s.BatchId == e.listing.BatchId, ct);
                    if (stock?.Quantities != null)
                    {
                        var root = stock.Quantities.RootElement;
                        var total = root.TryGetProperty("quantity", out var tq) ? tq.GetInt32() : 0;
                        var reserved = root.TryGetProperty("reserved_quantity", out var rq) ? rq.GetInt32() : 0;
                        var available = root.TryGetProperty("available_quantity", out var aq) ? aq.GetInt32() : 0;

                        if (available < e.reqItem.Quantity)
                        {
                            var productName = e.title ?? e.reqItem.ListingId.ToString();
                            throw new BadRequestException($"Insufficient stock for '{productName}'. Available: {available}, requested: {e.reqItem.Quantity}.");
                        }

                        reserved += e.reqItem.Quantity;
                        available -= e.reqItem.Quantity;

                        stock.Quantities = JsonDocument.Parse(JsonSerializer.Serialize(new
                        {
                            quantity = total,
                            reserved_quantity = reserved,
                            available_quantity = available
                        }));
                        stock.UpdatedAt = DateTime.UtcNow;
                    }
                }

                orderItems.Add(new OrderItem
                {
                    Id = Guid.NewGuid(),
                    ListingId = e.reqItem.ListingId,
                    BatchId = e.listing.BatchId,
                    BranchId = e.listing.BranchId,
                    Quantity = e.reqItem.Quantity,
                    Pricing = JsonDocument.Parse(JsonSerializer.Serialize(new { unit_price = e.unitPrice, subtotal = itemSubtotal.ToString("0") })),
                    Snapshots = JsonDocument.Parse(JsonSerializer.Serialize(new { title_snapshot = e.title, image_snapshot = e.image }))
                });
            }

            // 3. Resolve voucher (applied once to the whole order)
            decimal totalDiscount = 0;
            Voucher? voucher = null;

            if (!string.IsNullOrEmpty(req.VoucherCode))
            {
                voucher = await _context.Vouchers.FirstOrDefaultAsync(v => v.Code == req.VoucherCode && v.IsActive, ct);
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
                        minOrder = rulesRoot.TryGetProperty("min_order_amount", out var mo) && mo.ValueKind == JsonValueKind.String ? decimal.Parse(mo.GetString() ?? "0") : 0;
                    }

                    decimal eligibleTotal = voucher.BranchId.HasValue
                        ? enrichedItems.Where(e => e.listing.BranchId == voucher.BranchId).Sum(e => decimal.Parse(e.unitPrice) * e.reqItem.Quantity)
                        : cartSubtotal;

                    if (usedCount < usageLimit && eligibleTotal >= minOrder)
                    {
                        if (voucher.Info != null)
                        {
                            var infoRoot = voucher.Info.RootElement;
                            var type = infoRoot.TryGetProperty("discount_type", out var dt) ? dt.GetString() : null;
                            var valStr = infoRoot.TryGetProperty("discount_value", out var dv) ? dv.GetString() ?? "0" : "0";
                            var val = decimal.Parse(valStr);

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
                        FromDistrictId = 3695, // Default: Thủ Đức, HCM
                        FromWardCode = "90737",
                        ToDistrictId = req.DeliveryAddress.DistrictId,
                        ToWardCode = req.DeliveryAddress.WardCode,
                        Weight = orderItems.Sum(oi => oi.Quantity) * 1000,
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
                TypeInfo = JsonDocument.Parse(JsonSerializer.Serialize(new { order_type = req.OrderType, fulfillment_method = req.FulfillmentMethod })),
                Financials = JsonDocument.Parse(JsonSerializer.Serialize(new { subtotal = cartSubtotal.ToString("0"), shipping = shippingFee.ToString("0"), discount = totalDiscount.ToString("0"), tax = "0", total = orderTotal.ToString("0") })),
                Status = "pending",
                Notes = !string.IsNullOrEmpty(req.CustomerNote) ? JsonDocument.Parse(JsonSerializer.Serialize(new { customer_note = req.CustomerNote })) : null,
                DeliveryAddress = req.DeliveryAddress != null ? JsonDocument.Parse(JsonSerializer.Serialize(new { recipient_name = req.DeliveryAddress.RecipientName, phone = req.DeliveryAddress.Phone, address_line_1 = req.DeliveryAddress.AddressLine1, city = req.DeliveryAddress.City, district_id = req.DeliveryAddress.DistrictId, ward_code = req.DeliveryAddress.WardCode })) : null,
                CreatedAt = DateTime.UtcNow,
                OrderItems = orderItems
            };

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
    public UpdateOrderStatusHandler(IApplicationDbContext context) => _context = context;

    public async Task<OrderResponse> Handle(UpdateOrderStatusCommand cmd, CancellationToken ct)
    {
        var order = await _context.OrderHeaders.Include(o => o.OrderItems).FirstOrDefaultAsync(o => o.Id == cmd.Id, ct)
            ?? throw new NotFoundException($"Order {cmd.Id} not found.");

        order.Status = cmd.Request.Status;
        if (cmd.Request.Status == "confirmed") order.ConfirmedAt = DateTime.UtcNow;

        var notesDict = new Dictionary<string, object?>();
        if (order.Notes != null)
        {
            foreach (var p in order.Notes.RootElement.EnumerateObject())
                notesDict[p.Name] = p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() : p.Value.GetRawText();
        }

        if (!string.IsNullOrEmpty(cmd.Request.InternalNote)) notesDict["internal_note"] = cmd.Request.InternalNote;
        if (!string.IsNullOrEmpty(cmd.Request.RejectionReason)) notesDict["rejection_reason"] = cmd.Request.RejectionReason;
        if (!string.IsNullOrEmpty(cmd.Request.TrackingCode)) notesDict["tracking_code"] = cmd.Request.TrackingCode;
        if (!string.IsNullOrEmpty(cmd.Request.CarrierName)) notesDict["carrier_name"] = cmd.Request.CarrierName;

        order.Notes = JsonDocument.Parse(JsonSerializer.Serialize(notesDict));

        await _context.SaveChangesAsync(ct);
        return CreateOrderHandler.MapToResponse(order);
    }
}

public class CancelOrderHandler : IRequestHandler<CancelOrderCommand, OrderResponse>
{
    private readonly IApplicationDbContext _context;
    public CancelOrderHandler(IApplicationDbContext context) => _context = context;

    public async Task<OrderResponse> Handle(CancelOrderCommand cmd, CancellationToken ct)
    {
        var order = await _context.OrderHeaders.Include(o => o.OrderItems).FirstOrDefaultAsync(o => o.Id == cmd.Id, ct)
            ?? throw new NotFoundException($"Order {cmd.Id} not found.");

        if (order.Status != "pending")
            throw new BadRequestException("Only pending orders can be cancelled.");

        order.Status = "cancelled";
        var notes = new Dictionary<string, object?>();
        if (order.Notes != null)
            foreach (var p in order.Notes.RootElement.EnumerateObject())
                notes[p.Name] = p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() : p.Value.GetRawText();
        notes["cancellation_reason"] = cmd.Request.CancellationReason;
        order.Notes = JsonDocument.Parse(JsonSerializer.Serialize(notes));
        
        // Restore stock — must use BatchId (matching CreateOrderHandler which reserves by BatchId)
        if (order.OrderItems != null)
        {
            foreach (var item in order.OrderItems)
            {
                if (item.BatchId.HasValue)
                {
                    var stock = await _context.BatchStocks
                        .FirstOrDefaultAsync(s => s.BatchId == item.BatchId, ct);
                    if (stock != null && stock.Quantities != null)
                    {
                        var root = stock.Quantities.RootElement;
                        var total = root.TryGetProperty("quantity", out var t) ? t.GetInt32() : 0;
                        var reserved = root.TryGetProperty("reserved_quantity", out var r) ? r.GetInt32() : 0;
                        var available = root.TryGetProperty("available_quantity", out var a) ? a.GetInt32() : 0;
                        
                        // Deduct from reserved and add back to available
                        var q = item.Quantity;
                        reserved -= q;
                        if (reserved < 0) reserved = 0;
                        available += q;
                        
                        stock.Quantities = JsonDocument.Parse(JsonSerializer.Serialize(new
                        {
                            quantity = total,
                            reserved_quantity = reserved,
                            available_quantity = available
                        }));
                    }
                }
            }
        }

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
        
        // 1. Calculate totals
        decimal cartSubtotal = req.Items.Sum(i => i.Quantity * i.UnitPrice);
        if (cartSubtotal <= 0)
        {
            throw new Exception("Order subtotal must be greater than 0");
        }

        // 2. Validate deposit (must be >= 30%)
        decimal minDeposit = cartSubtotal * 0.3m;
        if (req.DepositAmount < minDeposit)
        {
            throw new Exception($"Deposit amount must be at least 30% ({minDeposit:N0} VND)");
        }
        
        decimal remainingBalance = cartSubtotal - req.DepositAmount;
        if (remainingBalance < 0) remainingBalance = 0;

        var orderCode = $"BOPIS-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpper()}";
        var orderId = Guid.NewGuid();

        // 3. Create Order Items and Stock Transfers
        var orderItems = new List<OrderItem>();
        foreach (var itemReq in req.Items)
        {
            // Note: BranchId is currently null for revenue purpose (awaiting admin approval)
            var orderItem = new OrderItem
            {
                Id = Guid.NewGuid(),
                OrderId = orderId,
                ListingId = itemReq.ListingId,
                Quantity = itemReq.Quantity,
                Pricing = JsonDocument.Parse(JsonSerializer.Serialize(new { 
                    unit_price = itemReq.UnitPrice.ToString("0"), 
                    subtotal = (itemReq.Quantity * itemReq.UnitPrice).ToString("0") 
                }))
            };
            orderItems.Add(orderItem);

            // Create a pending stock transfer (Sourcing request)
            var stockTransfer = new StockTransfer
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
            };
            _context.StockTransfers.Add(stockTransfer);
        }

        // 4. Create Order Header
        var order = new OrderHeader
        {
            Id = orderId,
            OrderCode = orderCode,
            TypeInfo = JsonDocument.Parse(JsonSerializer.Serialize(new { 
                order_type = "offline", 
                fulfillment_method = "bopis_transfer" 
            })),
            Financials = JsonDocument.Parse(JsonSerializer.Serialize(new { 
                subtotal = cartSubtotal.ToString("0"), 
                discount = "0", 
                tax = "0", 
                shipping = "0",
                total = cartSubtotal.ToString("0"),
                amount_paid = req.DepositAmount.ToString("0"),
                remaining_balance = remainingBalance.ToString("0")
            })),
            Status = "deposit_paid", 
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

        _context.OrderHeaders.Add(order);
        await _context.SaveChangesAsync(ct);

        return CreateOrderHandler.MapToResponse(order);
    }
}
