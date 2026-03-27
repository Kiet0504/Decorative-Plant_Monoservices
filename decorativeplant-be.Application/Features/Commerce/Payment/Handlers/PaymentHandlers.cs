using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using decorativeplant_be.Application.Common.DTOs.Commerce;
using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Commerce.Payment.Commands;
using decorativeplant_be.Application.Features.Commerce.Payment.Queries;
using decorativeplant_be.Application.Services;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Application.Features.Commerce.Payment.Handlers;

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

        var result = await _payOS.CreatePaymentLinkAsync(payosOrderCode, totalAmount, desc, payOSItems, cmd.Request.ReturnUrl, cmd.Request.CancelUrl, ct);

        // We arbitrarily attach the PaymentTransaction to the first order for foreign key purposes,
        // but store ALL order ids in the Details JSON.
        var entity = new PaymentTransaction
        {
            Id = Guid.NewGuid(), OrderId = firstOrder.Id,
            TransactionCode = $"PAY-{Guid.NewGuid().ToString()[..8].ToUpper()}",
            Details = JsonDocument.Parse(JsonSerializer.Serialize(new
            {
                provider = "payos", method = "bank_transfer", type = "payment",
                amount = totalAmount.ToString(), status = "pending",
                external_id = result.PaymentLinkId, checkout_url = result.CheckoutUrl,
                qr_code = result.QrCode, payos_order_code = payosOrderCode,
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

    public HandlePayOSWebhookHandler(
        IApplicationDbContext context, 
        IPayOSService payOS, 
        ILogger<HandlePayOSWebhookHandler> logger, 
        IEmailTemplateService emailTemplateService)
    { 
        _context = context; 
        _payOS = payOS; 
        _logger = logger; 
        _emailTemplateService = emailTemplateService;
    }

    public async Task<bool> Handle(HandlePayOSWebhookCommand cmd, CancellationToken ct)
    {
        if (cmd.Webhook.Data == null) return false;
        
        // Serialize the data back to JSON string for signature verification
        var dataString = JsonSerializer.Serialize(cmd.Webhook.Data);
        if (!_payOS.VerifyWebhookSignature(dataString, cmd.Webhook.Signature))
        {
            _logger.LogWarning("Invalid webhook signature for order code: {OrderCode}", cmd.Webhook.Data.OrderCode);
            throw new BadRequestException("Invalid webhook signature.");
        }

        var orderCode = cmd.Webhook.Data.OrderCode;
        
        // Optimization: Lazily load matching payment
        var payment = _context.PaymentTransactions.AsEnumerable().FirstOrDefault(p =>
        {
            if (p.Details == null) return false;
            return p.Details.RootElement.TryGetProperty("payos_order_code", out var poc) && 
                   poc.ValueKind == JsonValueKind.Number && 
                   poc.GetInt64() == orderCode;
        });
        if (payment == null) return false;

        var details = new Dictionary<string, object?>();
        if (payment.Details != null)
            foreach (var prop in payment.Details.RootElement.EnumerateObject())
                details[prop.Name] = prop.Value.ValueKind == JsonValueKind.String ? prop.Value.GetString() : prop.Value.GetRawText();
        
        var isSuccess = cmd.Webhook.Code == "00";
        details["status"] = isSuccess ? "paid" : "failed";
        payment.Details = JsonDocument.Parse(JsonSerializer.Serialize(details));

        if (isSuccess)
        {
            var orderIdsJson = payment.Details?.RootElement.GetProperty("order_ids");
            if (orderIdsJson.HasValue && orderIdsJson.Value.ValueKind == JsonValueKind.Array)
            {
                var orderIds = orderIdsJson.Value.EnumerateArray().Select(e => e.GetGuid()).ToList();
                var orders = await _context.OrderHeaders
                    .Include(o => o.Branch)
                    .Include(o => o.User)
                    .Where(o => orderIds.Contains(o.Id))
                    .ToListAsync(ct);

                foreach (var order in orders)
                {
                    if (order.Status == "pending") 
                    { 
                        order.Status = "confirmed"; 
                        order.ConfirmedAt = DateTime.UtcNow; 

                        // Send Email Notification
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
                                    { "BranchName", order.Branch?.Name ?? "Decorative Plant Store" },
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
                            // Don't fail the whole transaction if email fails
                        }
                    }
                }
            }
        }
        await _context.SaveChangesAsync(ct);
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
