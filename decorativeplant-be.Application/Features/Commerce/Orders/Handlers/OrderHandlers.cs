using System.Globalization;
using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using decorativeplant_be.Application.Common;
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
    private readonly IEmailService _emailService;
    private readonly IEmailTemplateService _emailTemplateService;
    private readonly IOrderAssignmentService _orderAssignment;

    public CreateOrderHandler(
        IApplicationDbContext context,
        ILogger<CreateOrderHandler> logger,
        IShippingService shippingService,
        IBranchAllocationService allocationService,
        IStockService stockService,
        IEmailService emailService,
        IEmailTemplateService emailTemplateService,
        IOrderAssignmentService orderAssignment)
    {
        _context = context;
        _logger = logger;
        _shippingService = shippingService;
        _allocationService = allocationService;
        _stockService = stockService;
        _emailService = emailService;
        _emailTemplateService = emailTemplateService;
        _orderAssignment = orderAssignment;
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

            // Map allocations to enriched items
            var enrichedItems = allocations.Select(a => (
                reqItem: new CreateOrderItemRequest { ListingId = a.Listing.Id, Quantity = a.AllocatedQuantity },
                listing: a.Listing,
                unitPrice: a.UnitPrice,
                title: a.Title,
                image: a.Image
            )).ToList();

            // 2. Reserve stock for all items and calculate subtotal
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
            }

            // 3. Resolve voucher (applied once across all branches/orders)
            decimal totalDiscount = 0;
            Voucher? voucher = null;
            Guid? appliedVoucherId = null;

            if (!string.IsNullOrEmpty(req.VoucherCode))
            {
                var pending = await _context.Vouchers.FirstOrDefaultAsync(v => v.Code == req.VoucherCode && v.IsActive, ct);
                if (pending != null)
                {
                    await _context.AcquireVoucherLockAsync(pending.Id, ct);
                    voucher = await _context.Vouchers.FirstOrDefaultAsync(v => v.Id == pending.Id, ct);
                }
                if (voucher != null)
                {
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
                    int userLimit = int.MaxValue;
                    decimal minOrder = 0;
                    decimal maxDiscount = 0;
                    List<Guid>? applicableProducts = null;

                    if (voucher.Rules != null)
                    {
                        var rulesRoot = voucher.Rules.RootElement;
                        usageLimit = rulesRoot.TryGetProperty("usage_limit", out var ul) && ul.ValueKind == JsonValueKind.Number ? ul.GetInt32() : int.MaxValue;
                        userLimit = rulesRoot.TryGetProperty("user_limit", out var uul) && uul.ValueKind == JsonValueKind.Number ? uul.GetInt32() : int.MaxValue;
                        usedCount = rulesRoot.TryGetProperty("used_count", out var uc) && uc.ValueKind == JsonValueKind.Number ? uc.GetInt32() : 0;
                        minOrder = rulesRoot.TryGetProperty("min_order_amount", out var mo) && mo.ValueKind == JsonValueKind.String ? decimal.Parse(mo.GetString() ?? "0", CultureInfo.InvariantCulture) : 0;
                        if (rulesRoot.TryGetProperty("maximum_discount", out var md) && md.ValueKind == JsonValueKind.String
                            && decimal.TryParse(md.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var mdVal))
                            maxDiscount = mdVal;
                        if (rulesRoot.TryGetProperty("applicable_products", out var ap) && ap.ValueKind == JsonValueKind.Array)
                        {
                            applicableProducts = ap.EnumerateArray()
                                .Select(el => el.ValueKind == JsonValueKind.String && Guid.TryParse(el.GetString(), out var g) ? g : (Guid?)null)
                                .Where(g => g.HasValue).Select(g => g!.Value).ToList();
                            if (applicableProducts.Count == 0) applicableProducts = null;
                        }
                    }

                    // Per-user limit: count prior non-voided orders this user has claimed with this voucher.
                    // Terminal refund/cancel paths roll used_count back via VoucherUsageHelper, so "active"
                    // orders here means anything not in cancelled/returned/refunded/expired.
                    if (userLimit < int.MaxValue && cmd.UserId != Guid.Empty)
                    {
                        var userUsed = await _context.OrderHeaders.CountAsync(o =>
                            o.UserId == cmd.UserId && o.VoucherId == voucher.Id
                            && o.Status != "cancelled" && o.Status != "returned"
                            && o.Status != "refunded" && o.Status != "expired", ct);
                        if (userUsed >= userLimit)
                        {
                            _logger.LogWarning("Voucher {Code} denied for user {UserId}: per-user limit {Limit} reached.",
                                req.VoucherCode, cmd.UserId, userLimit);
                            voucher = null;
                        }
                    }

                    // applicableProducts: restrict eligible subtotal to listings in the whitelist.
                    IEnumerable<(CreateOrderItemRequest reqItem, ProductListing listing, string unitPrice, string? title, string? image)> eligibleItems =
                        voucher != null && voucher.BranchId.HasValue
                            ? enrichedItems.Where(e => e.listing.BranchId == voucher.BranchId)
                            : enrichedItems;
                    if (voucher != null && applicableProducts != null)
                        eligibleItems = eligibleItems.Where(e => applicableProducts.Contains(e.listing.Id));

                    decimal eligibleTotal = voucher != null
                        ? eligibleItems.Sum(e => decimal.Parse(e.unitPrice, CultureInfo.InvariantCulture) * e.reqItem.Quantity)
                        : 0;

                    if (voucher != null && eligibleTotal <= 0 && applicableProducts != null)
                    {
                        _logger.LogWarning("Voucher {Code} denied: no cart items match applicable_products whitelist.", req.VoucherCode);
                        voucher = null;
                    }

                    if (voucher != null && usedCount < usageLimit && eligibleTotal >= minOrder)
                    {
                        if (voucher.Info != null)
                        {
                            var infoRoot = voucher.Info.RootElement;
                            var type = infoRoot.TryGetProperty("discount_type", out var dt) ? dt.GetString() : null;
                            var valStr = infoRoot.TryGetProperty("discount_value", out var dv) ? dv.GetString() ?? "0" : "0";
                            var val = decimal.Parse(valStr, CultureInfo.InvariantCulture);

                            if (type == "percentage")
                            {
                                totalDiscount = eligibleTotal * (val / 100);
                                // Cap percentage discount to maximum_discount when set (0 = unlimited).
                                if (maxDiscount > 0 && totalDiscount > maxDiscount) totalDiscount = maxDiscount;
                            }
                            else if (type == "fixed_amount" || type == "fixed")
                            {
                                totalDiscount = val;
                            }
                            // free_shipping discount is applied to shipping fee, not subtotal — handled below.
                        }

                        if (totalDiscount > eligibleTotal) totalDiscount = eligibleTotal;

                        if (voucher.Rules != null)
                        {
                            var rules = new Dictionary<string, object?>();
                            foreach (var p in voucher.Rules.RootElement.EnumerateObject())
                            {
                                rules[p.Name] = p.Value.ValueKind switch
                                {
                                    JsonValueKind.String => p.Value.GetString(),
                                    JsonValueKind.Number => p.Value.TryGetInt64(out var l) ? (object)l : p.Value.GetDouble(),
                                    JsonValueKind.Array => JsonSerializer.Deserialize<object?>(p.Value.GetRawText()),
                                    _ => p.Value.GetRawText()
                                };
                            }
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

            // 4. Group enriched items by branch
            var itemsByBranch = enrichedItems.GroupBy(e => e.listing.BranchId).ToList();

            // 5. Calculate shipping fees (1 per branch or use provided fee)
            var shippingFeesByBranch = new Dictionary<Guid, decimal>();
            decimal totalShippingFee = 0;

            foreach (var branchGroup in itemsByBranch)
            {
                var branchId = branchGroup.Key;
                decimal branchShippingFee = 30000; // default

                if (req.DeliveryAddress != null)
                {
                    try
                    {
                        var branchItems = branchGroup.ToList();
                        var feeResult = await _shippingService.CalculateFeeAsync(new ShippingFeeRequest
                        {
                            FromDistrictId = _shippingService.DefaultFromDistrictId,
                            FromWardCode = _shippingService.DefaultFromWardCode,
                            ToDistrictId = req.DeliveryAddress.DistrictId,
                            ToWardCode = req.DeliveryAddress.WardCode,
                            Weight = Math.Max(branchItems.Sum(e => e.reqItem.Quantity) * 500, 500),
                            InsuranceValue = (int)branchItems.Sum(e => decimal.Parse(e.unitPrice, CultureInfo.InvariantCulture) * e.reqItem.Quantity),
                            ServiceTypeId = 2
                        });
                        if (feeResult.Success && feeResult.Total > 0) branchShippingFee = feeResult.Total;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "GHN fee calc failed for branch {BranchId}, using default 30000", branchId);
                    }
                }
                else if (req.ShippingFee > 0)
                {
                    branchShippingFee = req.ShippingFee / itemsByBranch.Count; // distribute provided fee across branches
                }

                branchShippingFee = Math.Round(branchShippingFee, 0);
                shippingFeesByBranch[branchId ?? Guid.Empty] = branchShippingFee;
                totalShippingFee += branchShippingFee;
            }

            totalShippingFee = Math.Round(totalShippingFee, 0);
            totalDiscount = Math.Round(totalDiscount, 0);

            // 6. Create one OrderHeader per branch
            var orders = new List<OrderHeader>();
            var baseOrderTimestamp = DateTime.UtcNow;

            // Two-pass discount allocation: round each branch except the last, then give
            // the last branch whatever remainder is needed so the sum equals totalDiscount
            // to the cent. Avoids the "refunded 99 when customer was promised 100" drift.
            var orderedBranches = itemsByBranch.ToList();
            var branchSubtotals = orderedBranches.ToDictionary(
                g => g.Key ?? Guid.Empty,
                g => g.Sum(e => decimal.Parse(e.unitPrice, CultureInfo.InvariantCulture) * e.reqItem.Quantity));
            var branchDiscounts = new Dictionary<Guid, decimal>();
            if (totalDiscount > 0 && cartSubtotal > 0)
            {
                decimal allocated = 0;
                for (int i = 0; i < orderedBranches.Count; i++)
                {
                    var key = orderedBranches[i].Key ?? Guid.Empty;
                    decimal share = i == orderedBranches.Count - 1
                        ? totalDiscount - allocated
                        : Math.Round(totalDiscount * (branchSubtotals[key] / cartSubtotal), 0);
                    branchDiscounts[key] = share;
                    allocated += share;
                }
            }

            foreach (var branchGroup in orderedBranches)
            {
                var branchId = branchGroup.Key;
                var branchEnrichedItems = branchGroup.ToList();
                var branchOrderItems = new List<OrderItem>();
                decimal branchSubtotal = 0;

                foreach (var e in branchEnrichedItems)
                {
                    if (!decimal.TryParse(e.unitPrice, CultureInfo.InvariantCulture, out var parsedPrice))
                        throw new BadRequestException($"Invalid price for '{e.title}'.");

                    var itemSubtotal = parsedPrice * e.reqItem.Quantity;
                    branchSubtotal += itemSubtotal;

                    branchOrderItems.Add(new OrderItem
                    {
                        Id = Guid.NewGuid(),
                        ListingId = e.reqItem.ListingId,
                        BatchId = e.listing.BatchId,
                        BranchId = branchId,
                        Quantity = e.reqItem.Quantity,
                        Pricing = JsonDocument.Parse(JsonSerializer.Serialize(new { unit_price = e.unitPrice, subtotal = itemSubtotal.ToString("0", CultureInfo.InvariantCulture) })),
                        Snapshots = JsonDocument.Parse(JsonSerializer.Serialize(new { title_snapshot = e.title, image_snapshot = e.image }))
                    });
                }

                var branchShippingFee = shippingFeesByBranch[branchId ?? Guid.Empty];

                // Allocate discount proportionally to branch (pre-computed above)
                var branchDiscount = branchDiscounts.TryGetValue(branchId ?? Guid.Empty, out var bd) ? bd : 0;

                decimal branchOrderTotal = branchSubtotal + branchShippingFee - branchDiscount;
                if (branchOrderTotal < 0) branchOrderTotal = 0;

                var orderCode = $"ORD-{baseOrderTimestamp:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpper()}";

                var order = new OrderHeader
                {
                    Id = Guid.NewGuid(),
                    OrderCode = orderCode,
                    UserId = cmd.UserId,
                    VoucherId = appliedVoucherId,
                    TypeInfo = JsonDocument.Parse(JsonSerializer.Serialize(new { order_type = req.OrderType, fulfillment_method = req.FulfillmentMethod, payment_method = NormalizePaymentMethod(req.PaymentMethod), branch_id = branchId })),
                    Financials = JsonDocument.Parse(JsonSerializer.Serialize(new { subtotal = branchSubtotal.ToString("0", CultureInfo.InvariantCulture), shipping = branchShippingFee.ToString("0", CultureInfo.InvariantCulture), discount = branchDiscount.ToString("0", CultureInfo.InvariantCulture), tax = "0", total = branchOrderTotal.ToString("0", CultureInfo.InvariantCulture) })),
                    Status = "pending",
                    Notes = !string.IsNullOrEmpty(req.CustomerNote) ? JsonDocument.Parse(JsonSerializer.Serialize(new { customer_note = req.CustomerNote })) : null,
                    DeliveryAddress = req.DeliveryAddress != null ? JsonDocument.Parse(JsonSerializer.Serialize(new { recipient_name = req.DeliveryAddress.RecipientName, phone = req.DeliveryAddress.Phone, address_line_1 = req.DeliveryAddress.AddressLine1, city = req.DeliveryAddress.City, district_id = req.DeliveryAddress.DistrictId, ward_code = req.DeliveryAddress.WardCode })) : null,
                    CreatedAt = baseOrderTimestamp,
                    OrderItems = branchOrderItems
                };

                // Seed status history
                OrderStatusMachine.AppendHistory(order, from: null, to: OrderStatusMachine.Pending,
                    changedBy: cmd.UserId, reason: "Order created", source: "CreateOrder");

                _context.OrderHeaders.Add(order);
                orders.Add(order);

                // COD flow: auto-confirm and create GHN shipment
                if (NormalizePaymentMethod(req.PaymentMethod) == "cod")
                {
                    OrderStatusMachine.Apply(order, OrderStatusMachine.Confirmed,
                        changedBy: cmd.UserId, reason: "COD auto-confirm", source: "CreateOrder");
                    await GhnOrderHelper.TryCreateGhnOrderAsync(order, _shippingService, _logger);
                }
            }
            
            // Clear items from user's shopping cart immediately after order creation.
            // This ensures both COD and pending PayOS orders do not keep items in the cart.
            if (cmd.UserId != Guid.Empty && orders.Count > 0)
            {
                var cart = await _context.ShoppingCarts.FirstOrDefaultAsync(c => c.UserId == cmd.UserId, ct);
                if (cart != null && cart.Items != null)
                {
                    var cartItems = decorativeplant_be.Application.Features.Commerce.ShoppingCart.Handlers.AddToCartHandler.DeserializeItems(cart.Items);
                    
                    // Collect all listing IDs that were ordered across all branches
                    var purchasedListingIds = orders.SelectMany(o => o.OrderItems)
                        .Where(oi => oi.ListingId.HasValue)
                        .Select(oi => oi.ListingId!.Value)
                        .ToList();

                    if (purchasedListingIds.Any())
                    {
                        cartItems.RemoveAll(ci => purchasedListingIds.Contains(ci.ListingId));
                        cart.Items = decorativeplant_be.Application.Features.Commerce.ShoppingCart.Handlers.AddToCartHandler.SerializeItems(cartItems);
                        cart.UpdatedAt = DateTime.UtcNow;
                    }
                }
            }

            _logger.LogInformation("Created {OrderCount} split orders from {BranchCount} branch(es) for user {UserId}",
                orders.Count, itemsByBranch.Count, cmd.UserId);

            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            // Notify branch staff for COD orders (bank_transfer notifications fire
            // later in the PayOS webhook handler, once payment is actually confirmed).
            // Fire-and-log — never block the response on email.
            var user = cmd.UserId != Guid.Empty ? await _context.UserAccounts.FirstOrDefaultAsync(u => u.Id == cmd.UserId, ct) : null;
            var customerEmail = user?.Email;

            foreach (var order in orders.Where(o => o.Status == OrderStatusMachine.Confirmed))
            {
                if (!string.IsNullOrEmpty(customerEmail) && user != null)
                {
                    try
                    {
                        var total = "0";
                        if (order.Financials != null)
                        {
                            total = order.Financials.RootElement.TryGetProperty("total", out var t) ? t.GetString() ?? "0" : "0";
                        }

                        var model = new Dictionary<string, string>
                        {
                            { "CustomerName", user.DisplayName ?? "Customer" },
                            { "OrderCode", order.OrderCode ?? "N/A" },
                            { "BranchName", "Decorative Plant Store" },
                            { "Total", total }
                        };

                        await _emailTemplateService.SendTemplateAsync(
                            "OrderConfirmed",
                            model,
                            customerEmail,
                            $"Order Confirmed - {order.OrderCode}",
                            user.DisplayName,
                            ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send order confirmation email for {OrderCode}", order.OrderCode);
                    }
                }

                try
                {
                    await NewOrderForStaffNotifier.NotifyAsync(order, _context, _emailService, _logger, _orderAssignment, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Staff notify failed for Order {OrderCode}", order.OrderCode);
                }
            }

            return orders.Select(MapToResponse).ToList();
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
        }); // end strategy.ExecuteAsync
    }

    internal static string NormalizePaymentMethod(string? raw)
    {
        var v = (raw ?? "").Trim().ToLowerInvariant();
        return v switch
        {
            "cod" or "cash_on_delivery" or "cash-on-delivery" => "cod",
            _ => "bank_transfer"
        };
    }

    internal static OrderResponse MapToResponse(OrderHeader o)
    {
        var response = new OrderResponse
        {
            Id = o.Id, OrderCode = o.OrderCode, UserId = o.UserId,
            Status = o.Status ?? "pending", CreatedAt = o.CreatedAt, ConfirmedAt = o.ConfirmedAt,
            AssignedStaffId = o.AssignedStaffId,
            AssignedStaffName = o.AssignedStaff?.DisplayName
        };

        if (o.TypeInfo != null)
        {
            var root = o.TypeInfo.RootElement;
            response.OrderType = root.TryGetProperty("order_type", out var ot) ? ot.GetString() : null;
            response.FulfillmentMethod = root.TryGetProperty("fulfillment_method", out var fm) ? fm.GetString() : null;
            response.PaymentMethod = root.TryGetProperty("payment_method", out var pm) ? pm.GetString() : null;
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
    private static readonly HashSet<string> SlotFreedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "delivered", "completed", "cancelled", "returned", "refunded", "expired",
    };

    private readonly IApplicationDbContext _context;
    private readonly IStockService _stockService;
    private readonly IShippingService _shippingService;
    private readonly ILogger<UpdateOrderStatusHandler> _logger;
    private readonly IOrderAssignmentService _orderAssignment;

    public UpdateOrderStatusHandler(IApplicationDbContext context, IStockService stockService, IShippingService shippingService, ILogger<UpdateOrderStatusHandler> logger, IOrderAssignmentService orderAssignment)
    {
        _context = context;
        _stockService = stockService;
        _shippingService = shippingService;
        _logger = logger;
        _orderAssignment = orderAssignment;
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

        // When a slot frees up, immediately try to assign the oldest queued order
        if (SlotFreedStatuses.Contains(normalizedStatus) && order.AssignedStaffId.HasValue)
        {
            var branchId = order.OrderItems?.FirstOrDefault()?.BranchId;
            if (branchId.HasValue)
            {
                try { await _orderAssignment.TryFlushQueueAsync(branchId.Value, ct); }
                catch (Exception ex) { _logger.LogError(ex, "Queue flush failed after status update for Order {OrderCode}.", order.OrderCode); }
            }
        }

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
    private readonly IOrderAssignmentService _orderAssignment;
    private readonly ILogger<ConfirmReceiptHandler> _logger;

    public ConfirmReceiptHandler(IApplicationDbContext context, IOrderAssignmentService orderAssignment, ILogger<ConfirmReceiptHandler> logger)
    {
        _context = context;
        _orderAssignment = orderAssignment;
        _logger = logger;
    }

    public async Task<OrderResponse> Handle(ConfirmReceiptCommand cmd, CancellationToken ct)
    {
        var order = await _context.OrderHeaders.Include(o => o.OrderItems).FirstOrDefaultAsync(o => o.Id == cmd.OrderId, ct)
            ?? throw new NotFoundException($"Order {cmd.OrderId} not found.");

        if (order.UserId != cmd.UserId)
            throw new BadRequestException("You can only confirm receipt for your own orders.");

        // Idempotent: clients retry on flaky networks and the "complete" tap can double-fire in UI.
        // Returning the existing state keeps the contract of "after this call, order is Completed"
        // without re-invoking transition side-effects (queue flush, completed_at bump).
        if (order.Status == OrderStatusMachine.Completed)
            return CreateOrderHandler.MapToResponse(order);

        var branchId = order.OrderItems?.FirstOrDefault()?.BranchId;
        var wasAssigned = order.AssignedStaffId.HasValue;

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

        // Slot freed — assign next queued order at this branch immediately
        if (wasAssigned && branchId.HasValue)
        {
            try { await _orderAssignment.TryFlushQueueAsync(branchId.Value, ct); }
            catch (Exception ex) { _logger.LogError(ex, "Queue flush failed after ConfirmReceipt for Order {OrderId}.", cmd.OrderId); }
        }

        return CreateOrderHandler.MapToResponse(order);
    }
}

public class ConfirmReceiptBatchHandler : IRequestHandler<ConfirmReceiptBatchCommand, List<OrderResponse>>
{
    private readonly IApplicationDbContext _context;
    public ConfirmReceiptBatchHandler(IApplicationDbContext context) => _context = context;

    public async Task<List<OrderResponse>> Handle(ConfirmReceiptBatchCommand cmd, CancellationToken ct)
    {
        if (cmd.OrderIds == null || !cmd.OrderIds.Any())
            throw new BadRequestException("At least one OrderId is required.");

        var orders = await _context.OrderHeaders
            .Include(o => o.OrderItems)
            .Where(o => cmd.OrderIds.Contains(o.Id))
            .ToListAsync(ct);

        if (orders.Count == 0)
            throw new NotFoundException("None of the requested orders were found.");

        // Validate ownership — user can only confirm their own orders
        foreach (var order in orders)
        {
            if (order.UserId != cmd.UserId)
                throw new BadRequestException($"Order {order.OrderCode} does not belong to you.");
        }

        var completedAt = DateTime.UtcNow.ToString("o");

        // Confirm each order
        foreach (var order in orders)
        {
            // Skip orders already completed — keeps the batch idempotent under retry.
            if (order.Status == OrderStatusMachine.Completed)
                continue;

            OrderStatusMachine.Apply(order, OrderStatusMachine.Completed, cmd.UserId,
                reason: "Customer confirmed receipt", source: "ConfirmReceiptBatch");

            var notesDict = new Dictionary<string, object?>();
            if (order.Notes != null)
                foreach (var p in order.Notes.RootElement.EnumerateObject())
                {
                    if (p.Value.ValueKind == JsonValueKind.String) notesDict[p.Name] = p.Value.GetString();
                    else notesDict[p.Name] = JsonSerializer.Deserialize<object?>(p.Value.GetRawText());
                }
            notesDict["completed_at"] = completedAt;

            order.Notes = JsonDocument.Parse(JsonSerializer.Serialize(notesDict));
        }

        await _context.SaveChangesAsync(ct);
        return orders.Select(CreateOrderHandler.MapToResponse).ToList();
    }
}

public class GetOrdersHandler : IRequestHandler<GetOrdersQuery, PagedResult<OrderResponse>>
{
    private readonly IApplicationDbContext _context;
    public GetOrdersHandler(IApplicationDbContext context) => _context = context;

    public async Task<PagedResult<OrderResponse>> Handle(GetOrdersQuery query, CancellationToken ct)
    {
        var q = _context.OrderHeaders.Include(o => o.OrderItems).Include(o => o.AssignedStaff).AsQueryable();
        if (query.UserId.HasValue) q = q.Where(o => o.UserId == query.UserId);
        // Chain Store: filter by branch at OrderItem level
        if (query.BranchId.HasValue) q = q.Where(o => o.OrderItems.Any(oi => oi.BranchId == query.BranchId));
        if (query.AssignedStaffId.HasValue) q = q.Where(o => o.AssignedStaffId == query.AssignedStaffId);
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
        var order = await _context.OrderHeaders.Include(o => o.OrderItems).Include(o => o.AssignedStaff).FirstOrDefaultAsync(o => o.Id == query.Id, ct);
        if (order == null) return null;

        // Customer: must own the order
        if (query.UserId.HasValue && order.UserId != query.UserId)
            throw new BadRequestException("You do not have permission to view this order.");

        // Staff/manager: order must belong to their branch
        if (query.ActorBranchId.HasValue)
        {
            var belongsToBranch = order.OrderItems?.Any(oi => oi.BranchId == query.ActorBranchId) ?? false;
            if (!belongsToBranch)
                throw new BadRequestException("You do not have permission to view this order.");
        }

        // Fulfillment staff: order must be assigned to them
        if (query.ActorStaffId.HasValue && order.AssignedStaffId != query.ActorStaffId)
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

public class ManualAssignOrderHandler : IRequestHandler<ManualAssignOrderCommand, OrderResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IEmailService _emailService;
    private readonly ILogger<ManualAssignOrderHandler> _logger;

    public ManualAssignOrderHandler(
        IApplicationDbContext context,
        IEmailService emailService,
        ILogger<ManualAssignOrderHandler> logger)
    {
        _context = context;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<OrderResponse> Handle(ManualAssignOrderCommand cmd, CancellationToken ct)
    {
        var order = await _context.OrderHeaders
            .Include(o => o.OrderItems)
            .FirstOrDefaultAsync(o => o.Id == cmd.OrderId, ct)
            ?? throw new NotFoundException($"Order {cmd.OrderId} not found.");

        var branchId = order.OrderItems?.FirstOrDefault()?.BranchId
            ?? throw new BadRequestException("Order has no branch associated.");

        // Verify manager belongs to this branch
        var managerAtBranch = await _context.StaffAssignments
            .AnyAsync(sa => sa.StaffId == cmd.ManagerId && sa.BranchId == branchId, ct);
        if (!managerAtBranch)
            throw new BadRequestException("You do not manage the branch this order belongs to.");

        // Verify target staff is fulfillment_staff at the same branch
        var staffValid = await (
            from sa in _context.StaffAssignments
            join u in _context.UserAccounts on sa.StaffId equals u.Id
            where sa.StaffId == cmd.StaffId
               && sa.BranchId == branchId
               && u.Role == "fulfillment_staff"
               && u.IsActive
            select u
        ).AnyAsync(ct);

        if (!staffValid)
            throw new BadRequestException("The selected staff is not an active fulfillment_staff at this branch.");

        // Allow re-assignment too (manager override) — not just unassigned orders
        order.AssignedStaffId = cmd.StaffId;
        await _context.SaveChangesAsync(ct);

        // Notify the newly assigned staff
        var staff = await _context.UserAccounts.FindAsync([cmd.StaffId], ct);
        if (staff?.Email != null)
        {
            var total = order.Financials?.RootElement.TryGetProperty("total", out var t) == true
                ? t.GetString() ?? "0" : "0";
            var itemCount = order.OrderItems?.Sum(i => i.Quantity) ?? 0;
            var branchName = await _context.Branches
                .Where(b => b.Id == branchId)
                .Select(b => b.Name)
                .FirstOrDefaultAsync(ct) ?? "your branch";

            try
            {
                // Extract delivery address
                string? recipientName = null, phone = null, addressLine = null, city = null;
                if (order.DeliveryAddress != null)
                {
                    var addr = order.DeliveryAddress.RootElement;
                    recipientName = addr.TryGetProperty("recipient_name", out var rn) ? rn.GetString() : null;
                    phone         = addr.TryGetProperty("phone", out var ph) ? ph.GetString() : null;
                    addressLine   = addr.TryGetProperty("address_line_1", out var a1) ? a1.GetString() : null;
                    city          = addr.TryGetProperty("city", out var c) ? c.GetString() : null;
                }

                string? fulfillmentMethod = null, paymentMethod = null;
                if (order.TypeInfo != null)
                {
                    var ti = order.TypeInfo.RootElement;
                    fulfillmentMethod = ti.TryGetProperty("fulfillment_method", out var fm) ? fm.GetString() : null;
                    paymentMethod     = ti.TryGetProperty("payment_method", out var pm) ? pm.GetString() : null;
                }

                var itemRows = new System.Text.StringBuilder();
                foreach (var oi in order.OrderItems ?? [])
                {
                    string? title = null, unitPrice = null;
                    if (oi.Snapshots != null)
                        title = oi.Snapshots.RootElement.TryGetProperty("title_snapshot", out var ts) ? ts.GetString() : null;
                    if (oi.Pricing != null)
                        unitPrice = oi.Pricing.RootElement.TryGetProperty("unit_price", out var up) ? up.GetString() : null;
                    itemRows.Append(
                        $"<tr><td style='padding:4px 8px;border-bottom:1px solid #e5e7eb;'>{System.Net.WebUtility.HtmlEncode(title ?? "—")}</td>" +
                        $"<td style='padding:4px 8px;border-bottom:1px solid #e5e7eb;text-align:center;'>{oi.Quantity}</td>" +
                        $"<td style='padding:4px 8px;border-bottom:1px solid #e5e7eb;text-align:right;'>{System.Net.WebUtility.HtmlEncode(unitPrice ?? "—")} VND</td></tr>");
                }

                var deliveryHtml = recipientName != null
                    ? $"<tr><th style='text-align:left;padding:4px 8px;color:#6b7280;font-weight:normal;'>Recipient</th>" +
                      $"<td style='padding:4px 8px;'>{System.Net.WebUtility.HtmlEncode(recipientName)}" +
                      (phone != null ? $" · {System.Net.WebUtility.HtmlEncode(phone)}" : "") + "</td></tr>" +
                      $"<tr><th style='text-align:left;padding:4px 8px;color:#6b7280;font-weight:normal;'>Address</th>" +
                      $"<td style='padding:4px 8px;'>{System.Net.WebUtility.HtmlEncode(addressLine ?? "—")}" +
                      (city != null ? $", {System.Net.WebUtility.HtmlEncode(city)}" : "") + "</td></tr>"
                    : "";

                var bodyHtml =
                    $"<div style='font-family:sans-serif;max-width:600px;margin:0 auto;color:#1f2937;'>" +
                    $"<p>Hello <strong>{System.Net.WebUtility.HtmlEncode(staff.DisplayName ?? "")}</strong>,</p>" +
                    $"<p>Order <strong>{System.Net.WebUtility.HtmlEncode(order.OrderCode ?? order.Id.ToString())}</strong> at <strong>{System.Net.WebUtility.HtmlEncode(branchName)}</strong> has been <strong style='color:#2d5f4d;'>manually assigned to you</strong> by your branch manager.</p>" +
                    $"<table style='width:100%;border-collapse:collapse;margin:12px 0;'>" +
                    $"<tr><th style='text-align:left;padding:4px 8px;color:#6b7280;font-weight:normal;'>Order code</th><td style='padding:4px 8px;font-weight:bold;'>{System.Net.WebUtility.HtmlEncode(order.OrderCode ?? order.Id.ToString())}</td></tr>" +
                    $"<tr><th style='text-align:left;padding:4px 8px;color:#6b7280;font-weight:normal;'>Fulfillment</th><td style='padding:4px 8px;'>{System.Net.WebUtility.HtmlEncode(fulfillmentMethod ?? "delivery")}</td></tr>" +
                    $"<tr><th style='text-align:left;padding:4px 8px;color:#6b7280;font-weight:normal;'>Payment</th><td style='padding:4px 8px;'>{System.Net.WebUtility.HtmlEncode(paymentMethod ?? "—")}</td></tr>" +
                    deliveryHtml +
                    $"<tr><th style='text-align:left;padding:4px 8px;color:#6b7280;font-weight:normal;'>Total</th><td style='padding:4px 8px;font-weight:bold;color:#2d5f4d;'>{System.Net.WebUtility.HtmlEncode(total)} VND</td></tr>" +
                    $"</table>" +
                    (itemRows.Length > 0
                        ? $"<p style='margin-top:16px;font-weight:bold;'>Order items ({itemCount}):</p>" +
                          $"<table style='width:100%;border-collapse:collapse;font-size:14px;'>" +
                          $"<thead><tr style='background:#f3f4f6;'><th style='padding:4px 8px;text-align:left;'>Product</th><th style='padding:4px 8px;text-align:center;'>Qty</th><th style='padding:4px 8px;text-align:right;'>Unit price</th></tr></thead>" +
                          $"<tbody>{itemRows}</tbody></table>"
                        : "") +
                    "<p style='margin-top:20px;'>Please check the staff dashboard to begin packing.</p>" +
                    "</div>";

                var plainText =
                    $"Hi {staff.DisplayName ?? "staff"},\n\n" +
                    $"Order {order.OrderCode} at {branchName} has been manually assigned to you by your branch manager.\n" +
                    (recipientName != null ? $"Recipient: {recipientName} {phone}\nAddress: {addressLine}, {city}\n" : "") +
                    $"Items: {itemCount} | Total: {total} VND | Payment: {paymentMethod ?? "—"}\n\n" +
                    "Please log in to the staff dashboard to begin packing.";

                await _emailService.SendAsync(new Application.Common.DTOs.Email.EmailMessage
                {
                    To = staff.Email,
                    ToName = staff.DisplayName,
                    Subject = $"[Assigned to you] Order {order.OrderCode} — {branchName}",
                    BodyPlainText = plainText,
                    BodyHtml = bodyHtml,
                }, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ManualAssignOrderHandler: Failed to email staff {StaffId} for Order {OrderCode}.", cmd.StaffId, order.OrderCode);
            }
        }

        _logger.LogInformation(
            "ManualAssignOrderHandler: Order {OrderCode} manually assigned to staff {StaffId} by manager {ManagerId}.",
            order.OrderCode, cmd.StaffId, cmd.ManagerId);

        return CreateOrderHandler.MapToResponse(order);
    }
}
