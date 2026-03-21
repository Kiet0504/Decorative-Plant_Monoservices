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
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Application.Features.Commerce.Orders.Handlers;

public class CreateOrderHandler : IRequestHandler<CreateOrderCommand, List<OrderResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<CreateOrderHandler> _logger;
    private readonly IGhtkService _ghtkService;

    public CreateOrderHandler(IApplicationDbContext context, ILogger<CreateOrderHandler> logger, IGhtkService ghtkService)
    {
        _context = context;
        _logger = logger;
        _ghtkService = ghtkService;
    }

    public async Task<List<OrderResponse>> Handle(CreateOrderCommand cmd, CancellationToken ct)
    {
        var req = cmd.Request;

        using var transaction = await _context.Database.BeginTransactionAsync(ct);
        try
        {
            // 1. Fetch all listings and build enriched item list
            var enrichedItems = new List<(CreateOrderItemRequest reqItem, ProductListing listing, string unitPrice, string? title, string? image)>();
            foreach (var item in req.Items)
            {
                var listing = await _context.ProductListings.FindAsync(new object[] { item.ListingId }, ct)
                    ?? throw new NotFoundException($"Listing {item.ListingId} not found.");

                string unitPrice = "0";
                string? titleSnapshot = null;
                string? imageSnapshot = null;
                if (listing.ProductInfo != null)
                {
                    var root = listing.ProductInfo.RootElement;
                    unitPrice = root.TryGetProperty("price", out var p) ? p.GetString() ?? "0" : "0";
                    titleSnapshot = root.TryGetProperty("title", out var t) ? t.GetString() : null;
                }
                if (listing.Images?.RootElement.ValueKind == JsonValueKind.Array)
                {
                    var first = listing.Images.RootElement.EnumerateArray().FirstOrDefault();
                    imageSnapshot = first.TryGetProperty("url", out var u) ? u.GetString() : null;
                }

                enrichedItems.Add((item, listing, unitPrice, titleSnapshot, imageSnapshot));
            }

            // 2. Group items by BranchId
            var groupedByBranch = enrichedItems.GroupBy(e => e.listing.BranchId);

            // 3. Resolve voucher once (applied proportionally to each sub-order)
            decimal cartTotal = enrichedItems.Sum(e => decimal.Parse(e.unitPrice) * e.reqItem.Quantity);
            decimal totalDiscount = 0;
            Voucher? voucher = null;

            if (!string.IsNullOrEmpty(req.VoucherCode))
            {
                voucher = await _context.Vouchers.FirstOrDefaultAsync(v => v.Code == req.VoucherCode && v.IsActive, ct);
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

                    if (usedCount < usageLimit && cartTotal >= minOrder)
                    {
                        if (voucher.Info != null)
                        {
                            var infoRoot = voucher.Info.RootElement;
                            var type = infoRoot.TryGetProperty("discount_type", out var dt) ? dt.GetString() : null;
                            var valStr = infoRoot.TryGetProperty("discount_value", out var dv) ? dv.GetString() ?? "0" : "0";
                            var val = decimal.Parse(valStr);

                            if (type == "percentage") totalDiscount = cartTotal * (val / 100);
                            else if (type == "fixed") totalDiscount = val;
                        }

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

            // 4. Create one OrderHeader per branch
            var createdOrders = new List<OrderHeader>();

            foreach (var branchGroup in groupedByBranch)
            {
                var branchId = branchGroup.Key;
                var orderItems = new List<OrderItem>();
                decimal branchSubtotal = 0;

                foreach (var e in branchGroup)
                {
                    var itemSubtotal = decimal.Parse(e.unitPrice) * e.reqItem.Quantity;
                    branchSubtotal += itemSubtotal;

                    orderItems.Add(new OrderItem
                    {
                        Id = Guid.NewGuid(),
                        ListingId = e.reqItem.ListingId,
                        BatchId = e.listing.BatchId,
                        Quantity = e.reqItem.Quantity,
                        Pricing = JsonDocument.Parse(JsonSerializer.Serialize(new { unit_price = e.unitPrice, subtotal = itemSubtotal.ToString("0") })),
                        Snapshots = JsonDocument.Parse(JsonSerializer.Serialize(new { title_snapshot = e.title, image_snapshot = e.image }))
                    });
                }

                // Shipping & Discount
                decimal branchShipping = req.ShippingFee > 0 ? req.ShippingFee / groupedByBranch.Count() : 30000;
                decimal branchDiscount = cartTotal > 0 ? totalDiscount * (branchSubtotal / cartTotal) : 0;
                
                branchShipping = Math.Round(branchShipping, 0);
                branchDiscount = Math.Round(branchDiscount, 0);
                
                decimal branchTotal = branchSubtotal + branchShipping - branchDiscount;
                if (branchTotal < 0) branchTotal = 0;

                var orderCode = $"ORD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpper()}";

                var order = new OrderHeader
                {
                    Id = Guid.NewGuid(),
                    OrderCode = orderCode,
                    UserId = cmd.UserId,
                    BranchId = branchId,
                    TypeInfo = JsonDocument.Parse(JsonSerializer.Serialize(new { order_type = req.OrderType, fulfillment_method = req.FulfillmentMethod })),
                    Financials = JsonDocument.Parse(JsonSerializer.Serialize(new { subtotal = branchSubtotal.ToString("0"), shipping = branchShipping.ToString("0"), discount = branchDiscount.ToString("0"), tax = "0", total = branchTotal.ToString("0") })),
                    Status = "pending",
                    Notes = !string.IsNullOrEmpty(req.CustomerNote) ? JsonDocument.Parse(JsonSerializer.Serialize(new { customer_note = req.CustomerNote })) : null,
                    DeliveryAddress = req.DeliveryAddress != null ? JsonDocument.Parse(JsonSerializer.Serialize(new { recipient_name = req.DeliveryAddress.RecipientName, phone = req.DeliveryAddress.Phone, address_line_1 = req.DeliveryAddress.AddressLine1, city = req.DeliveryAddress.City })) : null,
                    CreatedAt = DateTime.UtcNow,
                    OrderItems = orderItems
                };

                // Call GHTK to create order and get tracking code
                if (req.DeliveryAddress != null)
                {
                    try
                    {
                        var branchAddress = branchGroup.FirstOrDefault().listing.Branch?.ContactInfo?.RootElement.TryGetProperty("address", out var ba) == true ? ba.GetString() : "Hồ Chí Minh, Thành Phố Thủ Đức";
                        
                        var ghtkProducts = orderItems.Select(oi => new GhtkProduct {
                            Name = "Cây cảnh",
                            Quantity = oi.Quantity,
                            Weight = 1.0 // 1kg default
                        }).ToList();

                        var ghtkOrderObj = new GhtkOrderInfo
                        {
                            Id = orderCode,
                            PickName = "Cửa hàng Decorative Plant",
                            PickAddress = branchAddress ?? "Hồ Chí Minh",
                            PickProvince = "Hồ Chí Minh",
                            PickDistrict = "Thủ Đức",
                            PickTel = "0900000000",
                            Name = req.DeliveryAddress.RecipientName,
                            Tel = req.DeliveryAddress.Phone,
                            Address = req.DeliveryAddress.AddressLine1,
                            Province = req.DeliveryAddress.City,
                            District = req.DeliveryAddress.City, // temporary
                            IsFreeship = 1,
                            Value = (int)branchTotal,
                            Transport = "road"
                        };

                        var ghtkRes = await _ghtkService.CreateOrderAsync(new GhtkOrderRequest { Products = ghtkProducts, Order = ghtkOrderObj });

                        if (ghtkRes.Success && ghtkRes.Order != null)
                        {
                            var notesObj = new Dictionary<string, string>
                            {
                                { "tracking_code", ghtkRes.Order.Label },
                                { "carrier_name", "GHTK" }
                            };
                            if (!string.IsNullOrEmpty(req.CustomerNote)) notesObj.Add("customer_note", req.CustomerNote);
                            
                            order.Notes = JsonDocument.Parse(JsonSerializer.Serialize(notesObj));
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to call GHTK CreateOrder for {OrderCode}", orderCode);
                    }
                }

                _context.OrderHeaders.Add(order);
                createdOrders.Add(order);
                _logger.LogInformation("Created Order {OrderCode} for Branch {BranchId}", orderCode, branchId);
            }

            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            // 5. Load branch names for response
            var branchIds = createdOrders.Where(o => o.BranchId.HasValue).Select(o => o.BranchId!.Value).Distinct().ToList();
            var branches = await _context.Branches.Where(b => branchIds.Contains(b.Id)).ToDictionaryAsync(b => b.Id, b => b.Name, ct);

            return createdOrders.Select(o => MapToResponse(o, branches)).ToList();
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    internal static OrderResponse MapToResponse(OrderHeader o, Dictionary<Guid, string>? branchNames = null)
    {
        var response = new OrderResponse
        {
            Id = o.Id, OrderCode = o.OrderCode, UserId = o.UserId, BranchId = o.BranchId,
            Status = o.Status ?? "pending", CreatedAt = o.CreatedAt, ConfirmedAt = o.ConfirmedAt
        };

        if (o.BranchId.HasValue && branchNames != null && branchNames.TryGetValue(o.BranchId.Value, out var name))
            response.BranchName = name;

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
                var item = new OrderItemResponse { Id = oi.Id, ListingId = oi.ListingId, StockId = oi.StockId, Quantity = oi.Quantity };
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
        
        // Restore stock
        if (order.OrderItems != null)
        {
            foreach (var item in order.OrderItems)
            {
                if (item.StockId.HasValue)
                {
                    var stock = await _context.BatchStocks.FindAsync(new object[] { item.StockId.Value }, ct);
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
        var q = _context.OrderHeaders.Include(o => o.OrderItems).Include(o => o.Branch).AsQueryable();
        if (query.UserId.HasValue) q = q.Where(o => o.UserId == query.UserId);
        if (query.BranchId.HasValue) q = q.Where(o => o.BranchId == query.BranchId);
        if (!string.IsNullOrEmpty(query.Status)) q = q.Where(o => o.Status == query.Status);

        var total = await q.CountAsync(ct);
        
        var orders = await q.OrderByDescending(o => o.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        var branchNames = orders.Where(o => o.Branch != null).Select(o => o.Branch!).DistinctBy(b => b.Id).ToDictionary(b => b.Id, b => b.Name);
            
        return new PagedResult<OrderResponse>
        {
            Items = orders.Select(o => CreateOrderHandler.MapToResponse(o, branchNames)).ToList(),
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
        var order = await _context.OrderHeaders.Include(o => o.OrderItems).Include(o => o.Branch).FirstOrDefaultAsync(o => o.Id == query.Id, ct);
        if (order == null) return null;
        var branchNames = order.Branch != null ? new Dictionary<Guid, string> { { order.Branch.Id, order.Branch.Name } } : null;
        return CreateOrderHandler.MapToResponse(order, branchNames);
    }
}
