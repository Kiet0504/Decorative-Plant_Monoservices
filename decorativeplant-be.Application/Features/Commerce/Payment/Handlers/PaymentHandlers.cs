using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using decorativeplant_be.Application.Common.DTOs.Commerce;
using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Commerce.Payment.Commands;
using decorativeplant_be.Application.Features.Commerce.Payment.Queries;
using decorativeplant_be.Application.Features.Commerce.ShoppingCart.Handlers;
using decorativeplant_be.Application.Services;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Application.Features.Commerce.Payment.Handlers;

using decorativeplant_be.Application.Features.Commerce.Orders;


public class CreatePaymentHandler : IRequestHandler<CreatePaymentCommand, PaymentResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IPayOSService _payOS;
    private readonly ILogger<CreatePaymentHandler> _logger;

    public CreatePaymentHandler(IApplicationDbContext context, IPayOSService payOS, ILogger<CreatePaymentHandler> logger)
    { _context = context; _payOS = payOS; _logger = logger; }

    public async Task<PaymentResponse> Handle(CreatePaymentCommand cmd, CancellationToken ct)
    {
        if (cmd.Request.OrderIds == null || !cmd.Request.OrderIds.Any())
            throw new BadRequestException("At least one OrderId is required.");

        var orders = await _context.OrderHeaders
            .Include(o => o.OrderItems)
            .Where(o => cmd.Request.OrderIds.Contains(o.Id))
            .ToListAsync(ct);

        if (orders.Count == 0)
            throw new NotFoundException($"None of the requested orders were found.");

        // Validate order ownership — user can only pay for their own orders
        foreach (var order in orders)
        {
            if (order.UserId != cmd.UserId)
                throw new BadRequestException($"Order {order.OrderCode} does not belong to you.");

            if (order.Status != "pending")
                throw new BadRequestException($"Order {order.OrderCode} is not in 'pending' status (current: {order.Status}).");
        }

        int totalAmount = 0;
        var payOSItems = new List<PayOSItem>();

        // Sum financials and collect items
        foreach (var order in orders)
        {
            if (order.Financials != null)
            {
                var totalStr = order.Financials.RootElement.TryGetProperty("total", out var t) ? t.GetString() ?? "0" : "0";
                totalAmount += (int)decimal.Parse(totalStr);
            }

            if (order.OrderItems != null)
            {
                foreach (var oi in order.OrderItems)
                {
                    string name = "Product"; int price = 0;
                    if (oi.Snapshots != null) name = oi.Snapshots.RootElement.TryGetProperty("title_snapshot", out var ts) ? ts.GetString() ?? "Product" : "Product";
                    if (oi.Pricing != null) price = (int)decimal.Parse(oi.Pricing.RootElement.TryGetProperty("unit_price", out var up) ? up.GetString() ?? "0" : "0");

                    // PayOS API limits items array size, compress if necessary
                    payOSItems.Add(new PayOSItem { Name = name.Length > 200 ? name[..200] : name, Quantity = oi.Quantity, Price = price });
                }
            }
        }

        // Generate a unified order code for PayOS
        var firstOrder = orders.First();
        var mainOrderCode = firstOrder.OrderCode ?? firstOrder.Id.ToString();
        // FIX #4: Use timestamp + random suffix instead of GetHashCode to prevent collision
        // GetHashCode() is non-deterministic across processes and can collide for different inputs
        var random = new Random();
        long payosOrderCode = (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % 1_000_000_000L) * 10 + random.Next(0, 10);

        var desc = $"TT cho {orders.Count} don hang";
        if (desc.Length > 25) desc = desc[..25]; // PayOS max length is 25

        string? buyerName = null;
        string? buyerPhone = null;
        string? buyerAddress = null;

        if (firstOrder.DeliveryAddress != null)
        {
            var da = firstOrder.DeliveryAddress.RootElement;
            if (da.TryGetProperty("recipientName", out var nameProp)) buyerName = nameProp.GetString();
            if (da.TryGetProperty("phone", out var phoneProp)) buyerPhone = phoneProp.GetString();
            
            string? line1 = null;
            string? city = null;
            if (da.TryGetProperty("addressLine1", out var l1Prop)) line1 = l1Prop.GetString();
            if (da.TryGetProperty("city", out var cProp)) city = cProp.GetString();

            if (!string.IsNullOrEmpty(line1) || !string.IsNullOrEmpty(city))
            {
                buyerAddress = $"{line1}, {city}".Trim(new char[] { ',', ' ' });
            }
        }

        var result = await _payOS.CreatePaymentLinkAsync(
            payosOrderCode, totalAmount, desc, payOSItems, 
            cmd.Request.ReturnUrl, cmd.Request.CancelUrl, 
            buyerName, null, buyerPhone, buyerAddress, ct);

        // We arbitrarily attach the PaymentTransaction to the first order for foreign key purposes,
        // but store ALL order ids in the Details JSON.
        var entity = new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            OrderId = firstOrder.Id,
            TransactionCode = $"PAY-{Guid.NewGuid().ToString()[..8].ToUpper()}",
            Details = JsonDocument.Parse(JsonSerializer.Serialize(new
            {
                provider = "payos",
                method = "bank_transfer",
                type = "payment",
                amount = totalAmount.ToString(),
                status = "pending",
                external_id = result.PaymentLinkId,
                checkout_url = result.CheckoutUrl,
                qr_code = result.QrCode,
                payos_order_code = payosOrderCode,
                order_ids = cmd.Request.OrderIds // Crucial: store list of all order ids
            })),
            CreatedAt = DateTime.UtcNow
        };
        _context.PaymentTransactions.Add(entity);
        await _context.SaveChangesAsync(ct);
        return MapToResponse(entity);
    }

    internal static PaymentResponse MapToResponse(PaymentTransaction e)
    {
        var r = new PaymentResponse { Id = e.Id, OrderId = e.OrderId, TransactionCode = e.TransactionCode, CreatedAt = e.CreatedAt };
        if (e.Details != null)
        {
            var root = e.Details.RootElement;
            r.Provider = root.TryGetProperty("provider", out var p) ? p.GetString() : null;
            r.Method = root.TryGetProperty("method", out var m) ? m.GetString() : null;
            r.Amount = root.TryGetProperty("amount", out var a) ? a.GetString() : null;
            r.Status = root.TryGetProperty("status", out var s) ? s.GetString() : null;
            r.ExternalId = root.TryGetProperty("external_id", out var ei) ? ei.GetString() : null;
            r.CheckoutUrl = root.TryGetProperty("checkout_url", out var cu) ? cu.GetString() : null;
            r.QrCode = root.TryGetProperty("qr_code", out var qr) ? qr.GetString() : null;
        }
        return r;
    }
}

public class HandlePayOSWebhookHandler : IRequestHandler<HandlePayOSWebhookCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly IPayOSService _payOS;
    private readonly ILogger<HandlePayOSWebhookHandler> _logger;
    private readonly IEmailTemplateService _emailTemplateService;
    private readonly IShippingService _shippingService;
    private readonly IStockService _stockService;

    public HandlePayOSWebhookHandler(
        IApplicationDbContext context,
        IPayOSService payOS,
        ILogger<HandlePayOSWebhookHandler> logger,
        IEmailTemplateService emailTemplateService,
        IShippingService shippingService,
        IStockService stockService)
    {
        _context = context;
        _payOS = payOS;
        _logger = logger;
        _emailTemplateService = emailTemplateService;
        _shippingService = shippingService;
        _stockService = stockService;
    }

    public async Task<bool> Handle(HandlePayOSWebhookCommand cmd, CancellationToken ct)
    {
        // When setting up the webhook in the PayOS dashboard, PayOS sends a verification webhook with Data = null.
        // We MUST return true for the webhook setup to succeed.
        if (cmd.Webhook.Data == null)
        {
            _logger.LogInformation("Received PayOS webhook verification/test request. Responding success.");
            return true;
        }

        // Dummy test payload from PayOS dashboard
        if (cmd.Webhook.Data.OrderCode == 123 || cmd.Webhook.Data.Description == "VQRIO123")
        {
            _logger.LogInformation("Received PayOS dummy test webhook for URL config. Responding success.");
            return true;
        }

        if (!_payOS.VerifyWebhookSignature(cmd.RawJsonBody))
        {
            _logger.LogWarning("Invalid webhook signature for order code: {OrderCode}", cmd.Webhook.Data.OrderCode);
            throw new BadRequestException("Invalid webhook signature.");
        }

        var orderCode = cmd.Webhook.Data.OrderCode ?? 0;

        // PayOS payment links expire in 30 minutes — only scan last 2 hours to avoid full table scan
        var cutoff = DateTime.UtcNow.AddHours(-2);
        var recentPayments = await _context.PaymentTransactions
            .Where(p => p.Details != null && p.CreatedAt >= cutoff)
            .ToListAsync(ct);

        var payment = recentPayments.FirstOrDefault(p =>
        {
            if (p.Details!.RootElement.TryGetProperty("payos_order_code", out var poc))
            {
                if (poc.ValueKind == JsonValueKind.Number && poc.GetInt64() == orderCode) return true;
                if (poc.ValueKind == JsonValueKind.String && long.TryParse(poc.GetString(), out var strval) && strval == orderCode) return true;
            }
            return false;
        });

        if (payment == null)
        {
            _logger.LogError("PayOS Webhook: No matching payment found for payos_order_code={OrderCode}. Order may remain pending.", orderCode);
            return false;
        }

        List<Guid> orderIdsList = new();
        if (payment.Details != null && payment.Details.RootElement.TryGetProperty("order_ids", out var originalOrderIdsJson))
        {
            if (originalOrderIdsJson.ValueKind == JsonValueKind.Array)
            {
                orderIdsList = originalOrderIdsJson.EnumerateArray().Select(e => e.GetGuid()).ToList();
            }
            else if (originalOrderIdsJson.ValueKind == JsonValueKind.String)
            {
                var str = originalOrderIdsJson.GetString();
                if (!string.IsNullOrEmpty(str))
                {
                    var parsed = JsonDocument.Parse(str);
                    orderIdsList = parsed.RootElement.EnumerateArray().Select(e => e.GetGuid()).ToList();
                }
            }
        }

        var details = new Dictionary<string, object?>();
        if (payment.Details != null)
        {
            foreach (var prop in payment.Details.RootElement.EnumerateObject())
            {
                if (prop.Name == "order_ids")
                    details[prop.Name] = orderIdsList;
                else
                    details[prop.Name] = prop.Value.ValueKind == JsonValueKind.String ? prop.Value.GetString() : prop.Value.GetRawText();
            }
        }

        var isSuccess = cmd.Webhook.Code == "00";
        details["status"] = isSuccess ? "paid" : "failed";
        payment.Details = JsonDocument.Parse(JsonSerializer.Serialize(details));

        // Wrap stock + order mutations in a transaction with pessimistic locking
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _context.Database.BeginTransactionAsync(ct);
            try
            {
                if (isSuccess)
                {
                    if (orderIdsList.Any())
                    {
                        var orders = await _context.OrderHeaders
                            .Include(o => o.User)
                            .Include(o => o.OrderItems)
                            .Where(o => orderIdsList.Contains(o.Id))
                            .ToListAsync(ct);

                        foreach (var order in orders)
                        {
                            if (order.Status == "pending")
                            {
                                // Remove items from user's cart
                                if (order.UserId != Guid.Empty)
                                {
                                    var cart = await _context.ShoppingCarts.FirstOrDefaultAsync(c => c.UserId == order.UserId, ct);
                                    if (cart != null && cart.Items != null && order.OrderItems != null)
                                    {
                                        var cartItems = AddToCartHandler.DeserializeItems(cart.Items);
                                        var purchasedListingIds = order.OrderItems.Where(oi => oi.ListingId.HasValue).Select(oi => oi.ListingId!.Value).ToList();

                                        if (purchasedListingIds.Any())
                                        {
                                            cartItems.RemoveAll(ci => purchasedListingIds.Contains(ci.ListingId));
                                            cart.Items = AddToCartHandler.SerializeItems(cartItems);
                                            cart.UpdatedAt = DateTime.UtcNow;
                                        }
                                    }
                                }

                                // Deduct stock with pessimistic locking
                                if (order.OrderItems != null)
                                    await _stockService.DeductOrderStockAsync(order.OrderItems, ct);
                            }
                            
                            var notesObj = new Dictionary<string, object?>();
                            if (order.Notes != null)
                            {
                                foreach (var prop in order.Notes.RootElement.EnumerateObject())
                                    notesObj[prop.Name] = prop.Value.ValueKind == JsonValueKind.String
                                        ? prop.Value.GetString() : prop.Value.GetRawText();
                            }
                            notesObj["payment_status"] = "paid";
                            order.Notes = JsonDocument.Parse(JsonSerializer.Serialize(notesObj));

                            // Create GHN shipments for paid orders
                            await GhnOrderHelper.TryCreateGhnOrderAsync(order, _shippingService, _logger);
                        }
                    }
                }
                else
                {
                    if (orderIdsList.Any())
                    {
                        var orders = await _context.OrderHeaders
                            .Include(o => o.OrderItems)
                            .Where(o => orderIdsList.Contains(o.Id))
                            .ToListAsync(ct);

                        foreach (var order in orders)
                        {
                            if (order.Status == "pending")
                            {
                                decorativeplant_be.Application.Features.Commerce.Orders.OrderStatusMachine
                                    .ApplyFromExternalSource(order,
                                        decorativeplant_be.Application.Features.Commerce.Orders.OrderStatusMachine.Cancelled,
                                        source: "PayOSWebhook",
                                        reason: $"Payment failed (Code: {cmd.Webhook.Code})");

                                var notes = new Dictionary<string, object?>();
                                if (order.Notes != null)
                                    foreach (var p in order.Notes.RootElement.EnumerateObject())
                                    {
                                        if (p.Value.ValueKind == JsonValueKind.String) notes[p.Name] = p.Value.GetString();
                                        else notes[p.Name] = JsonSerializer.Deserialize<object?>(p.Value.GetRawText());
                                    }

                                notes["cancellation_reason"] = $"Payment failed or cancelled via PayOS Webhook (Code: {cmd.Webhook.Code}).";
                                order.Notes = JsonDocument.Parse(JsonSerializer.Serialize(notes));

                                // Restore stock with pessimistic locking
                                if (order.OrderItems != null)
                                    await _stockService.RestoreOrderStockAsync(order.OrderItems, ct);
                            }
                        }
                    }
                }

                await _context.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        });

        // Send email notifications outside the transaction (non-critical)
        if (isSuccess && orderIdsList.Any())
        {
            var orders = await _context.OrderHeaders
                .Include(o => o.User)
                .Where(o => orderIdsList.Contains(o.Id))
                .ToListAsync(ct);

            foreach (var order in orders)
            {
                try
                {
                    if (order.User != null && !string.IsNullOrEmpty(order.User.Email))
                    {
                        var total = "0";
                        if (order.Financials != null)
                        {
                            total = order.Financials.RootElement.TryGetProperty("total", out var t) ? t.GetString() ?? "0" : "0";
                        }

                        var model = new Dictionary<string, string>
                        {
                            { "CustomerName", order.User.DisplayName ?? "Customer" },
                            { "OrderCode", order.OrderCode ?? "N/A" },
                            { "BranchName", "Decorative Plant Store" },
                            { "Total", total }
                        };

                        await _emailTemplateService.SendTemplateAsync(
                            "OrderConfirmed",
                            model,
                            order.User.Email,
                            $"Order Confirmed - {order.OrderCode}",
                            order.User.DisplayName,
                            ct);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send order confirmation email for {OrderCode}", order.OrderCode);
                }
            }
        }

        return true;
    }
}

public class GetPaymentsByOrderHandler : IRequestHandler<GetPaymentsByOrderQuery, List<PaymentResponse>>
{
    private readonly IApplicationDbContext _context;
    public GetPaymentsByOrderHandler(IApplicationDbContext context) => _context = context;
    public async Task<List<PaymentResponse>> Handle(GetPaymentsByOrderQuery q, CancellationToken ct) =>
        (await _context.PaymentTransactions.Where(p => p.OrderId == q.OrderId).OrderByDescending(p => p.CreatedAt).ToListAsync(ct)).Select(CreatePaymentHandler.MapToResponse).ToList();
}

public class GetPaymentByIdHandler : IRequestHandler<GetPaymentByIdQuery, PaymentResponse?>
{
    private readonly IApplicationDbContext _context;
    public GetPaymentByIdHandler(IApplicationDbContext context) => _context = context;
    public async Task<PaymentResponse?> Handle(GetPaymentByIdQuery q, CancellationToken ct)
    {
        var e = await _context.PaymentTransactions.FindAsync(new object[] { q.Id }, ct);
        return e == null ? null : CreatePaymentHandler.MapToResponse(e);
    }
}

public class SyncPaymentCommandHandler : IRequestHandler<SyncPaymentCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly IPayOSService _payOS;
    private readonly IShippingService _shippingService;
    private readonly IStockService _stockService;
    private readonly ILogger<SyncPaymentCommandHandler> _logger;

    public SyncPaymentCommandHandler(
        IApplicationDbContext context,
        IPayOSService payOS,
        IShippingService shippingService,
        IStockService stockService,
        ILogger<SyncPaymentCommandHandler> logger)
    {
        _context = context;
        _payOS = payOS;
        _shippingService = shippingService;
        _stockService = stockService;
        _logger = logger;
    }

    public async Task<bool> Handle(SyncPaymentCommand request, CancellationToken ct)
    {
        var payment = await _context.PaymentTransactions
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync(p => p.OrderId == request.OrderId, ct);

        if (payment == null || payment.Details == null) return false;

        if (payment.Details.RootElement.TryGetProperty("payos_order_code", out var poc) && poc.ValueKind == JsonValueKind.Number)
        {
            var code = poc.GetInt64();
            var info = await _payOS.GetPaymentInfoAsync(code, ct);
            if (info == null) return false;

            _logger.LogInformation("PayOS sync for order {OrderId}: PayOS status = '{Status}'", request.OrderId, info.Status);

            var payosStatus = info.Status?.Trim().ToUpperInvariant();
            if (payosStatus == "PAID" || payosStatus == "00" || payosStatus == "SUCCESS")
            {
                // Read order_ids BEFORE overwriting payment.Details
                var originalOrderIdsJson = payment.Details.RootElement.GetProperty("order_ids");
                List<Guid> orderIds;
                if (originalOrderIdsJson.ValueKind == JsonValueKind.Array)
                {
                    orderIds = originalOrderIdsJson.EnumerateArray().Select(e => e.GetGuid()).ToList();
                }
                else
                {
                    var parsed = JsonDocument.Parse(originalOrderIdsJson.GetString()!);
                    orderIds = parsed.RootElement.EnumerateArray().Select(e => e.GetGuid()).ToList();
                }

                var details = new Dictionary<string, object?>();
                foreach (var prop in payment.Details.RootElement.EnumerateObject())
                {
                    if (prop.Name == "order_ids")
                        details[prop.Name] = orderIds;
                    else
                        details[prop.Name] = prop.Value.ValueKind == JsonValueKind.String ? prop.Value.GetString() : prop.Value.GetRawText();
                }

                if (details["status"]?.ToString() == "paid") return true; // already synced

                details["status"] = "paid";
                payment.Details = JsonDocument.Parse(JsonSerializer.Serialize(details));

                // Wrap stock + order mutations in a transaction with pessimistic locking
                var strategy = _context.Database.CreateExecutionStrategy();
                await strategy.ExecuteAsync(async () =>
                {
                    using var transaction = await _context.Database.BeginTransactionAsync(ct);
                    try
                    {
                        var orders = await _context.OrderHeaders
                            .Include(o => o.OrderItems)
                            .Where(o => orderIds.Contains(o.Id))
                            .ToListAsync(ct);

                        foreach (var order in orders)
                        {
                            if (order.Status == "pending")
                            {
                                // Remove purchased items from user's cart
                                if (order.UserId.HasValue && order.UserId != Guid.Empty)
                                {
                                    var cart = await _context.ShoppingCarts.FirstOrDefaultAsync(c => c.UserId == order.UserId, ct);
                                    if (cart != null && cart.Items != null && order.OrderItems != null)
                                    {
                                        var cartItems = AddToCartHandler.DeserializeItems(cart.Items);
                                        var purchasedListingIds = order.OrderItems.Where(oi => oi.ListingId.HasValue).Select(oi => oi.ListingId!.Value).ToList();
                                        if (purchasedListingIds.Any())
                                        {
                                            cartItems.RemoveAll(ci => purchasedListingIds.Contains(ci.ListingId));
                                            cart.Items = AddToCartHandler.SerializeItems(cartItems);
                                            cart.UpdatedAt = DateTime.UtcNow;
                                        }
                                    }
                                }

                                // Deduct stock with pessimistic locking
                                if (order.OrderItems != null)
                                    await _stockService.DeductOrderStockAsync(order.OrderItems, ct);
                            }

                            var notesObj = new Dictionary<string, object?>();
                            if (order.Notes != null)
                            {
                                foreach (var prop in order.Notes.RootElement.EnumerateObject())
                                    notesObj[prop.Name] = prop.Value.ValueKind == JsonValueKind.String
                                        ? prop.Value.GetString() : prop.Value.GetRawText();
                            }
                            notesObj["payment_status"] = "paid";
                            order.Notes = JsonDocument.Parse(JsonSerializer.Serialize(notesObj));

                            // Create GHN shipments for paid orders
                            await GhnOrderHelper.TryCreateGhnOrderAsync(order, _shippingService, _logger);
                        }

                        await _context.SaveChangesAsync(ct);
                        await transaction.CommitAsync(ct);
                    }
                    catch
                    {
                        await transaction.RollbackAsync(ct);
                        throw;
                    }
                });

                return true;
            }
            else
            {
                _logger.LogWarning("PayOS sync: payment not yet paid. Status = '{Status}' for order {OrderId}", info.Status, request.OrderId);
            }
        }
        return false;
    }
}
