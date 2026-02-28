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
        var order = await _context.OrderHeaders.Include(o => o.OrderItems).FirstOrDefaultAsync(o => o.Id == cmd.Request.OrderId, ct)
            ?? throw new NotFoundException($"Order {cmd.Request.OrderId} not found.");

        int amount = 0;
        if (order.Financials != null)
        {
            var totalStr = order.Financials.RootElement.TryGetProperty("total", out var t) ? t.GetString() ?? "0" : "0";
            amount = (int)decimal.Parse(totalStr);
        }

        var payOSItems = new List<PayOSItem>();
        if (order.OrderItems != null)
            foreach (var oi in order.OrderItems)
            {
                string name = "Product"; int price = 0;
                if (oi.Snapshots != null) name = oi.Snapshots.RootElement.TryGetProperty("title_snapshot", out var ts) ? ts.GetString() ?? "Product" : "Product";
                if (oi.Pricing != null) price = (int)decimal.Parse(oi.Pricing.RootElement.TryGetProperty("unit_price", out var up) ? up.GetString() ?? "0" : "0");
                payOSItems.Add(new PayOSItem { Name = name, Quantity = oi.Quantity, Price = price });
            }

        var orderCode = Math.Abs(order.OrderCode?.GetHashCode() ?? order.Id.GetHashCode()) % 1_000_000_000L;
        var desc = $"TT {order.OrderCode}";
        if (desc.Length > 9) desc = desc[..9];

        var result = await _payOS.CreatePaymentLinkAsync(orderCode, amount, desc, payOSItems, cmd.Request.ReturnUrl, cmd.Request.CancelUrl, ct);

        var entity = new PaymentTransaction
        {
            Id = Guid.NewGuid(), OrderId = order.Id,
            TransactionCode = $"PAY-{Guid.NewGuid().ToString()[..8].ToUpper()}",
            Details = JsonDocument.Parse(JsonSerializer.Serialize(new
            {
                provider = "payos", method = "bank_transfer", type = "payment",
                amount = amount.ToString(), status = "pending",
                external_id = result.PaymentLinkId, checkout_url = result.CheckoutUrl,
                qr_code = result.QrCode, payos_order_code = orderCode
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

    public HandlePayOSWebhookHandler(IApplicationDbContext context, IPayOSService payOS, ILogger<HandlePayOSWebhookHandler> logger)
    { _context = context; _payOS = payOS; _logger = logger; }

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
        var orderCodeStr = orderCode.ToString();
        
        // Optimization: Lazily load matching payment instead of buffering all into memory
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

        if (isSuccess && payment.OrderId.HasValue)
        {
            var order = await _context.OrderHeaders.FindAsync(new object[] { payment.OrderId.Value }, ct);
            if (order != null && order.Status == "pending") { order.Status = "confirmed"; order.ConfirmedAt = DateTime.UtcNow; }
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
