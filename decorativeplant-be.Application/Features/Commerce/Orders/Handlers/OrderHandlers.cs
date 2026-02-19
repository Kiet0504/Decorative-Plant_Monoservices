using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using decorativeplant_be.Application.Common.DTOs.Commerce;
using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Commerce.Orders.Commands;
using decorativeplant_be.Application.Features.Commerce.Orders.Queries;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Application.Features.Commerce.Orders.Handlers;

public class CreateOrderHandler : IRequestHandler<CreateOrderCommand, OrderResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<CreateOrderHandler> _logger;

    public CreateOrderHandler(IApplicationDbContext context, ILogger<CreateOrderHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<OrderResponse> Handle(CreateOrderCommand cmd, CancellationToken ct)
    {
        var req = cmd.Request;
        var orderItems = new List<OrderItem>();
        decimal subtotal = 0;

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

            var itemSubtotal = decimal.Parse(unitPrice) * item.Quantity;
            subtotal += itemSubtotal;

            orderItems.Add(new OrderItem
            {
                Id = Guid.NewGuid(),
                ListingId = item.ListingId,
                BatchId = listing.BatchId,
                Quantity = item.Quantity,
                Pricing = JsonDocument.Parse(JsonSerializer.Serialize(new { unit_price = unitPrice, subtotal = itemSubtotal.ToString("0") })),
                Snapshots = JsonDocument.Parse(JsonSerializer.Serialize(new { title_snapshot = titleSnapshot, image_snapshot = imageSnapshot }))
            });
        }

        var orderCode = $"ORD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpper()}";

        var order = new OrderHeader
        {
            Id = Guid.NewGuid(),
            OrderCode = orderCode,
            UserId = cmd.UserId,
            BranchId = req.BranchId,
            TypeInfo = JsonDocument.Parse(JsonSerializer.Serialize(new { order_type = req.OrderType, fulfillment_method = req.FulfillmentMethod })),
            Financials = JsonDocument.Parse(JsonSerializer.Serialize(new { subtotal = subtotal.ToString("0"), shipping = "0", discount = "0", tax = "0", total = subtotal.ToString("0") })),
            Status = "pending",
            Notes = !string.IsNullOrEmpty(req.CustomerNote) ? JsonDocument.Parse(JsonSerializer.Serialize(new { customer_note = req.CustomerNote })) : null,
            DeliveryAddress = req.DeliveryAddress != null ? JsonDocument.Parse(JsonSerializer.Serialize(new { recipient_name = req.DeliveryAddress.RecipientName, phone = req.DeliveryAddress.Phone, address_line_1 = req.DeliveryAddress.AddressLine1, city = req.DeliveryAddress.City })) : null,
            CreatedAt = DateTime.UtcNow,
            OrderItems = orderItems
        };

        _context.OrderHeaders.Add(order);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Created Order {OrderCode}", orderCode);
        return MapToResponse(order);
    }

    internal static OrderResponse MapToResponse(OrderHeader o)
    {
        var response = new OrderResponse
        {
            Id = o.Id, OrderCode = o.OrderCode, UserId = o.UserId, BranchId = o.BranchId,
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
            response.CustomerNote = o.Notes.RootElement.TryGetProperty("customer_note", out var cn) ? cn.GetString() : null;
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

        if (!string.IsNullOrEmpty(cmd.Request.InternalNote) || !string.IsNullOrEmpty(cmd.Request.RejectionReason))
        {
            var notes = new Dictionary<string, object?>();
            if (order.Notes != null)
                foreach (var p in order.Notes.RootElement.EnumerateObject())
                    notes[p.Name] = p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() : p.Value.GetRawText();
            if (!string.IsNullOrEmpty(cmd.Request.InternalNote)) notes["internal_note"] = cmd.Request.InternalNote;
            if (!string.IsNullOrEmpty(cmd.Request.RejectionReason)) notes["rejection_reason"] = cmd.Request.RejectionReason;
            order.Notes = JsonDocument.Parse(JsonSerializer.Serialize(notes));
        }

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

        await _context.SaveChangesAsync(ct);
        return CreateOrderHandler.MapToResponse(order);
    }
}

public class GetOrdersHandler : IRequestHandler<GetOrdersQuery, List<OrderResponse>>
{
    private readonly IApplicationDbContext _context;
    public GetOrdersHandler(IApplicationDbContext context) => _context = context;

    public async Task<List<OrderResponse>> Handle(GetOrdersQuery query, CancellationToken ct)
    {
        var q = _context.OrderHeaders.Include(o => o.OrderItems).AsQueryable();
        if (query.UserId.HasValue) q = q.Where(o => o.UserId == query.UserId);
        if (query.BranchId.HasValue) q = q.Where(o => o.BranchId == query.BranchId);
        if (!string.IsNullOrEmpty(query.Status)) q = q.Where(o => o.Status == query.Status);

        var orders = await q.OrderByDescending(o => o.CreatedAt).ToListAsync(ct);
        return orders.Select(CreateOrderHandler.MapToResponse).ToList();
    }
}

public class GetOrderByIdHandler : IRequestHandler<GetOrderByIdQuery, OrderResponse?>
{
    private readonly IApplicationDbContext _context;
    public GetOrderByIdHandler(IApplicationDbContext context) => _context = context;

    public async Task<OrderResponse?> Handle(GetOrderByIdQuery query, CancellationToken ct)
    {
        var order = await _context.OrderHeaders.Include(o => o.OrderItems).FirstOrDefaultAsync(o => o.Id == query.Id, ct);
        return order == null ? null : CreateOrderHandler.MapToResponse(order);
    }
}
