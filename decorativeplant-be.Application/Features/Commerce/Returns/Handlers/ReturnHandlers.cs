using System.Globalization;
using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using decorativeplant_be.Application.Common.DTOs.Commerce;
using decorativeplant_be.Application.Common.DTOs.Common;
using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Commerce.Orders;
using decorativeplant_be.Application.Features.Commerce.Returns.Commands;
using decorativeplant_be.Application.Features.Commerce.Returns.Queries;
using decorativeplant_be.Application.Features.Commerce.Vouchers;
using decorativeplant_be.Application.Services;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Application.Features.Commerce.Returns.Handlers;

public class CreateReturnRequestHandler : IRequestHandler<CreateReturnRequestCommand, ReturnRequestResponse>
{
    // Window measured from DeliveredAt. Beyond this, the buyer must contact support — we don't
    // want indefinite return exposure on stock that may have already been restocked/aged out.
    private const int ReturnWindowDays = 7;

    private readonly IApplicationDbContext _context;
    public CreateReturnRequestHandler(IApplicationDbContext context) => _context = context;

    public async Task<ReturnRequestResponse> Handle(CreateReturnRequestCommand cmd, CancellationToken ct)
    {
        var req = cmd.Request;
        if (string.IsNullOrWhiteSpace(req.Reason))
            throw new BadRequestException("Reason is required.");

        var order = await _context.OrderHeaders.FirstOrDefaultAsync(o => o.Id == req.OrderId, ct)
            ?? throw new NotFoundException($"Order {req.OrderId} not found.");

        if (order.UserId != cmd.UserId)
            throw new BadRequestException("Order does not belong to the authenticated user.");

        if (order.Status != OrderStatusMachine.Delivered && order.Status != OrderStatusMachine.Completed)
            throw new BadRequestException("Order must be delivered or completed to request a return.");

        if (order.DeliveredAt is { } deliveredAt)
        {
            var deadline = deliveredAt.AddDays(ReturnWindowDays);
            if (DateTime.UtcNow > deadline)
                throw new BadRequestException(
                    $"Return window has expired. Returns must be requested within {ReturnWindowDays} days of delivery.");
        }

        var existing = await _context.ReturnRequests
            .AnyAsync(r => r.OrderId == req.OrderId && r.Status != "rejected", ct);
        if (existing)
            throw new BadRequestException("A return request already exists for this order.");

        var entity = new ReturnRequest
        {
            Id = Guid.NewGuid(),
            OrderId = req.OrderId,
            UserId = cmd.UserId,
            Status = "pending",
            Info = JsonDocument.Parse(JsonSerializer.Serialize(new
            {
                reason = req.Reason,
                description = req.Description,
            })),
            Images = req.Images?.Count > 0
                ? JsonDocument.Parse(JsonSerializer.Serialize(
                    req.Images.Select(i => new { url = i.Url, alt = i.Alt, sort = i.Sort })))
                : null,
            CreatedAt = DateTime.UtcNow,
        };
        _context.ReturnRequests.Add(entity);
        await _context.SaveChangesAsync(ct);

        return MapToResponse(entity, order.OrderCode);
    }

    internal static ReturnRequestResponse MapToResponse(ReturnRequest e, string? orderCode = null)
    {
        var r = new ReturnRequestResponse
        {
            Id = e.Id,
            OrderId = e.OrderId,
            OrderCode = orderCode,
            UserId = e.UserId,
            Status = e.Status ?? "pending",
            CreatedAt = e.CreatedAt,
        };

        if (e.Info != null)
        {
            var root = e.Info.RootElement;
            r.Reason = root.TryGetProperty("reason", out var reason) ? reason.GetString() : null;
            r.Description = root.TryGetProperty("description", out var desc) ? desc.GetString() : null;
            r.ResolutionNote = root.TryGetProperty("resolution_note", out var note) ? note.GetString() : null;
            r.ReturnShippingCode = root.TryGetProperty("return_shipping_code", out var rsc) ? rsc.GetString() : null;
            if (root.TryGetProperty("resolved_at", out var ra) && DateTime.TryParse(ra.GetString(), out var dt))
                r.ResolvedAt = dt;
        }

        if (e.Images?.RootElement.ValueKind == JsonValueKind.Array)
            r.Images = e.Images.RootElement.EnumerateArray().Select(img => new ReturnImageDto
            {
                Url = img.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "",
                Alt = img.TryGetProperty("alt", out var a) ? a.GetString() : null,
                Sort = img.TryGetProperty("sort", out var s) ? s.GetInt32() : 0,
            }).ToList();

        return r;
    }
}

public class UpdateReturnStatusHandler : IRequestHandler<UpdateReturnStatusCommand, ReturnRequestResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IStockService _stockService;
    private readonly IShippingService _shippingService;
    private readonly Microsoft.Extensions.Logging.ILogger<UpdateReturnStatusHandler> _logger;
    public UpdateReturnStatusHandler(IApplicationDbContext context, IStockService stockService, IShippingService shippingService, Microsoft.Extensions.Logging.ILogger<UpdateReturnStatusHandler> logger)
    {
        _context = context;
        _stockService = stockService;
        _shippingService = shippingService;
        _logger = logger;
    }

    public async Task<ReturnRequestResponse> Handle(UpdateReturnStatusCommand cmd, CancellationToken ct)
    {
        var status = (cmd.Request.Status ?? "").ToLowerInvariant();
        if (status is not ("pending" or "approved" or "rejected" or "refunded"))
            throw new BadRequestException($"Invalid return status '{cmd.Request.Status}'.");

        var entity = await _context.ReturnRequests.FirstOrDefaultAsync(r => r.Id == cmd.Id, ct)
            ?? throw new NotFoundException($"Return request {cmd.Id} not found.");

        var order = entity.OrderId.HasValue
            ? await _context.OrderHeaders.Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.Id == entity.OrderId.Value, ct)
            : null;

        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            using var tx = await _context.Database.BeginTransactionAsync(ct);
            try
            {
                entity.Status = status;

                var info = new Dictionary<string, object?>();
                if (entity.Info != null)
                {
                    foreach (var p in entity.Info.RootElement.EnumerateObject())
                    {
                        info[p.Name] = p.Value.ValueKind == JsonValueKind.String
                            ? p.Value.GetString()
                            : JsonSerializer.Deserialize<object?>(p.Value.GetRawText());
                    }
                }
                if (!string.IsNullOrWhiteSpace(cmd.Request.ResolutionNote))
                    info["resolution_note"] = cmd.Request.ResolutionNote;
                info["resolved_at"] = DateTime.UtcNow.ToString("o");
                info["resolved_by"] = cmd.ActorUserId?.ToString();
                entity.Info = JsonDocument.Parse(JsonSerializer.Serialize(info));

                // On approval: transition order to returned + restore stock + free the voucher.
                if (status == "approved" && order != null)
                {
                    if (cmd.Request.CreateGhnReturnOrder)
                    {
                        var ghnReturnCode = await TryCreateReturnShippingOrderAsync(order, _shippingService, _logger);
                        if (!string.IsNullOrEmpty(ghnReturnCode))
                        {
                            info["return_shipping_code"] = ghnReturnCode;
                        }
                    }

                    OrderStatusMachine.ApplyFromExternalSource(
                        order,
                        OrderStatusMachine.Returned,
                        source: "ReturnRequestApproved",
                        reason: cmd.Request.ResolutionNote ?? "Return approved");

                    if (order.OrderItems != null)
                        await _stockService.RestoreOrderStockAsync(order.OrderItems, ct);

                    if (order.VoucherId.HasValue)
                        await VoucherUsageHelper.RollbackUsageAsync(_context, order.VoucherId.Value, ct);

                    // Re-persist info because we may have added return_shipping_code
                    entity.Info = JsonDocument.Parse(JsonSerializer.Serialize(info));
                }

                // On refunded: book a pending offline refund PaymentTransaction so finance can
                // reconcile the money movement. PayOS has no programmatic refund API — the
                // actual payout happens out-of-band.
                if (status == "refunded" && order != null)
                {
                    var paidAmount = await ResolveActuallyPaidAmount(_context, order, ct);
                    if (paidAmount <= 0)
                    {
                        // COD orders that were cancelled/returned before pickup never collected
                        // money — refunding them would pay out cash the business never received.
                        throw new BadRequestException(
                            "Cannot mark this return as refunded: no completed payment found on the order (likely an unpaid COD). " +
                            "Use 'approved' instead, which restores stock without triggering a payout.");
                    }

                    _context.PaymentTransactions.Add(new PaymentTransaction
                    {
                        Id = Guid.NewGuid(),
                        OrderId = order.Id,
                        TransactionCode = $"REF-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpper()}",
                        Details = JsonDocument.Parse(JsonSerializer.Serialize(new
                        {
                            provider = ResolveProvider(order),
                            type = "refund",
                            amount = paidAmount.ToString("0", CultureInfo.InvariantCulture),
                            status = "pending",
                            reason = cmd.Request.ResolutionNote ?? "Return refunded",
                            refunded_by = cmd.ActorUserId,
                            source = "ReturnRequestRefunded",
                            return_id = entity.Id,
                        })),
                        CreatedAt = DateTime.UtcNow,
                    });
                }

                await _context.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        });

        return CreateReturnRequestHandler.MapToResponse(entity, order?.OrderCode);
    }

    /// <summary>
    /// Only count money that actually settled on this order. We look at PaymentTransactions with
    /// status=paid/completed and subtract any existing refund transactions — so unpaid COD orders
    /// (no settled payment) return 0 and the caller refuses to open a refund payout.
    /// </summary>
    private static async Task<decimal> ResolveActuallyPaidAmount(
        IApplicationDbContext context, OrderHeader order, CancellationToken ct)
    {
        var payments = await context.PaymentTransactions
            .Where(p => p.OrderId == order.Id && p.Details != null)
            .ToListAsync(ct);

        decimal paid = 0, refunded = 0;
        foreach (var p in payments)
        {
            if (p.Details == null) continue;
            var root = p.Details.RootElement;
            var type = root.TryGetProperty("type", out var tEl) ? tEl.GetString() : null;
            var status = root.TryGetProperty("status", out var sEl) ? sEl.GetString() : null;
            var amount = root.TryGetProperty("amount", out var aEl) ? aEl.GetString() : null;
            if (!decimal.TryParse(amount, NumberStyles.Any, CultureInfo.InvariantCulture, out var amt)) continue;

            var isRefund = string.Equals(type, "refund", StringComparison.OrdinalIgnoreCase);
            var settled = string.Equals(status, "paid", StringComparison.OrdinalIgnoreCase)
                       || string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase);

            if (isRefund) refunded += amt;
            else if (settled) paid += amt;
        }

        return Math.Max(0, paid - refunded);
    }

    private static string ResolveProvider(OrderHeader order)
    {
        if (order.TypeInfo != null
            && order.TypeInfo.RootElement.TryGetProperty("payment_method", out var pm))
        {
            var v = (pm.GetString() ?? "").Trim().ToLowerInvariant();
            if (v == "cod") return "offline";
        }
        return "payos";
    }

    private static async Task<string?> TryCreateReturnShippingOrderAsync(OrderHeader order, IShippingService shippingService, Microsoft.Extensions.Logging.ILogger logger)
    {
        logger.LogInformation("GHN: Starting RETURN shipment creation for Order {OrderCode}", order.OrderCode);

        if (order.DeliveryAddress == null)
        {
            logger.LogWarning("GHN: Skipping - DeliveryAddress is null for Order {OrderCode}", order.OrderCode);
            return null;
        }

        try
        {
            var da = order.DeliveryAddress.RootElement;
            string GetProp(params string[] keys)
            {
                foreach (var k in keys)
                {
                    if (!da.TryGetProperty(k, out var p)) continue;
                    var s = p.ValueKind == JsonValueKind.String ? p.GetString() : (p.ValueKind == JsonValueKind.Number ? p.GetRawText().Trim() : "");
                    if (!string.IsNullOrEmpty(s)) return s;
                }
                return "";
            }
            int GetIntProp(int def, params string[] keys)
            {
                foreach (var k in keys)
                {
                    if (!da.TryGetProperty(k, out var p)) continue;
                    if (p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var i)) return i;
                    if (p.ValueKind == JsonValueKind.String && int.TryParse(p.GetString(), out var v)) return v;
                }
                return def;
            }

            // Customer address becomes From
            var fromName = GetProp("recipientName", "recipient_name");
            var fromPhone = GetProp("phone");
            var fromAddress = GetProp("addressLine1", "address_line_1");
            var fromDistrict = GetIntProp(shippingService.DefaultFromDistrictId, "districtId", "district_id");
            var fromWard = GetProp("wardCode", "ward_code");
            if (string.IsNullOrEmpty(fromWard)) fromWard = shippingService.DefaultFromWardCode;

            if (string.IsNullOrEmpty(fromName)) fromName = "Customer Return";
            if (string.IsNullOrEmpty(fromPhone)) fromPhone = "0900000000";

            var ghnItems = order.OrderItems?.Select(oi => {
                var name = "Return - Decorative Plant";
                if (oi.Snapshots != null && oi.Snapshots.RootElement.TryGetProperty("title_snapshot", out var ts))
                    name = "Return - " + (ts.GetString() ?? "Plant");
                return new ShippingOrderItem { Name = name, Quantity = oi.Quantity, Weight = 500 };
            }).ToList() ?? new List<ShippingOrderItem> { new ShippingOrderItem { Name = "Return - Plant", Quantity = 1, Weight = 500 } };

            var clientCode = $"RETURN-{order.OrderCode ?? order.Id.ToString()}";

            var res = await shippingService.CreateOrderAsync(new ShippingOrderRequest {
                // Shop is To
                ToName = "Shop Decorative Plant",
                ToPhone = "0900000000",
                ToAddress = "Store Address",
                ToDistrictId = shippingService.DefaultFromDistrictId,
                ToWardCode = shippingService.DefaultFromWardCode,
                
                // Customer is From
                FromDistrictId = fromDistrict,
                FromWardCode = fromWard,
                
                Weight = Math.Max(ghnItems.Sum(i => i.Quantity) * 500, 500),
                InsuranceValue = 0, // No insurance for return to save costs
                ClientOrderCode = clientCode,
                Items = ghnItems,
                ServiceTypeId = shippingService.DefaultServiceTypeId,
                
                CodAmount = 0, // No COD for return
                PaymentTypeId = 1, // 1 = Seller pays shipping
                Note = $"Return for order {order.OrderCode}"
            });

            if (!res.Success || string.IsNullOrEmpty(res.OrderCode))
            {
                logger.LogError("GHN Error for Return Order {OrderCode}: {Message}", order.OrderCode, res.Message);
                return null;
            }

            logger.LogInformation("Successfully created GHN return order {TrackingCode} for {OrderCode}", res.OrderCode, order.OrderCode);
            return res.OrderCode;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GHN Error creating return for {OrderCode}", order.OrderCode);
            return null;
        }
    }
}

public class GetReturnByIdHandler : IRequestHandler<GetReturnByIdQuery, ReturnRequestResponse?>
{
    private readonly IApplicationDbContext _context;
    public GetReturnByIdHandler(IApplicationDbContext context) => _context = context;
    public async Task<ReturnRequestResponse?> Handle(GetReturnByIdQuery q, CancellationToken ct)
    {
        var e = await _context.ReturnRequests
            .Include(r => r.Order)
            .FirstOrDefaultAsync(r => r.Id == q.Id, ct);
        return e == null ? null : CreateReturnRequestHandler.MapToResponse(e, e.Order?.OrderCode);
    }
}

public class GetMyReturnsHandler : IRequestHandler<GetMyReturnsQuery, PagedResult<ReturnRequestResponse>>
{
    private readonly IApplicationDbContext _context;
    public GetMyReturnsHandler(IApplicationDbContext context) => _context = context;
    public async Task<PagedResult<ReturnRequestResponse>> Handle(GetMyReturnsQuery q, CancellationToken ct)
    {
        var query = _context.ReturnRequests
            .Include(r => r.Order)
            .Where(r => r.UserId == q.UserId);
        var total = await query.CountAsync(ct);
        var list = await query.OrderByDescending(r => r.CreatedAt)
            .Skip((q.Page - 1) * q.PageSize).Take(q.PageSize).ToListAsync(ct);
        return new PagedResult<ReturnRequestResponse>
        {
            Items = list.Select(e => CreateReturnRequestHandler.MapToResponse(e, e.Order?.OrderCode)).ToList(),
            TotalCount = total,
            Page = q.Page,
            PageSize = q.PageSize,
        };
    }
}

public class GetAllReturnsHandler : IRequestHandler<GetAllReturnsQuery, PagedResult<ReturnRequestResponse>>
{
    private readonly IApplicationDbContext _context;
    public GetAllReturnsHandler(IApplicationDbContext context) => _context = context;
    public async Task<PagedResult<ReturnRequestResponse>> Handle(GetAllReturnsQuery q, CancellationToken ct)
    {
        var query = _context.ReturnRequests.Include(r => r.Order).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q.Status))
            query = query.Where(r => r.Status == q.Status);
        var total = await query.CountAsync(ct);
        var list = await query.OrderByDescending(r => r.CreatedAt)
            .Skip((q.Page - 1) * q.PageSize).Take(q.PageSize).ToListAsync(ct);
        return new PagedResult<ReturnRequestResponse>
        {
            Items = list.Select(e => CreateReturnRequestHandler.MapToResponse(e, e.Order?.OrderCode)).ToList(),
            TotalCount = total,
            Page = q.Page,
            PageSize = q.PageSize,
        };
    }
}
