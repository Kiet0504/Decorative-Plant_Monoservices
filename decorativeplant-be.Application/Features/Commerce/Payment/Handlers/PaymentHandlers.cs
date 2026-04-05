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
    private readonly IShippingService _shippingService;

    public HandlePayOSWebhookHandler(
        IApplicationDbContext context, 
        IPayOSService payOS, 
        ILogger<HandlePayOSWebhookHandler> logger, 
        IEmailTemplateService emailTemplateService,
        IShippingService shippingService)
    { 
        _context = context; 
        _payOS = payOS; 
        _logger = logger; 
        _emailTemplateService = emailTemplateService;
        _shippingService = shippingService;
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

        // According to PayOS, webhook signature data string is built by picking non-null fields
        // from data, sorting them alphabetically by key, and connecting with '=' and '&'
        var dict = new SortedDictionary<string, string>();
        var wd = cmd.Webhook.Data;
        if (wd.Amount.HasValue) dict.Add("amount", wd.Amount.Value.ToString());
        if (wd.OrderCode.HasValue) dict.Add("orderCode", wd.OrderCode.Value.ToString());
        if (wd.AccountNumber != null) dict.Add("accountNumber", wd.AccountNumber);
        if (wd.Code != null) dict.Add("code", wd.Code);
        if (wd.CounterAccountBankId != null) dict.Add("counterAccountBankId", wd.CounterAccountBankId);
        if (wd.CounterAccountBankName != null) dict.Add("counterAccountBankName", wd.CounterAccountBankName);
        if (wd.CounterAccountName != null) dict.Add("counterAccountName", wd.CounterAccountName);
        if (wd.CounterAccountNumber != null) dict.Add("counterAccountNumber", wd.CounterAccountNumber);
        if (wd.Currency != null) dict.Add("currency", wd.Currency);
        if (wd.Desc != null) dict.Add("desc", wd.Desc);
        if (wd.Description != null) dict.Add("description", wd.Description);
        if (wd.PaymentLinkId != null) dict.Add("paymentLinkId", wd.PaymentLinkId);
        if (wd.Reference != null) dict.Add("reference", wd.Reference);
        if (wd.TransactionDateTime != null) dict.Add("transactionDateTime", wd.TransactionDateTime);
        if (wd.VirtualAccountName != null) dict.Add("virtualAccountName", wd.VirtualAccountName);
        if (wd.VirtualAccountNumber != null) dict.Add("virtualAccountNumber", wd.VirtualAccountNumber);

        var dataString = string.Join("&", dict.Select(kvp => $"{kvp.Key}={kvp.Value}"));

        if (!_payOS.VerifyWebhookSignature(dataString, cmd.Webhook.Signature ?? ""))
        {
            _logger.LogWarning("Invalid webhook signature for order code: {OrderCode}", cmd.Webhook.Data.OrderCode);
            throw new BadRequestException("Invalid webhook signature.");
        }

        var orderCode = cmd.Webhook.Data.OrderCode ?? 0;
        
        // Optimization: Lazily load matching payment
        var payment = _context.PaymentTransactions.AsEnumerable().FirstOrDefault(p =>
        {
            if (p.Details == null) return false;
            return p.Details.RootElement.TryGetProperty("payos_order_code", out var poc) && 
                   poc.ValueKind == JsonValueKind.Number && 
                   poc.GetInt64() == orderCode;
        });
        if (payment == null) return false;

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
                        order.Status = "confirmed"; 
                        order.ConfirmedAt = DateTime.UtcNow; 

                        // Create GHN shipping order now that payment is confirmed
                        await GhnOrderHelper.TryCreateGhnOrderAsync(order, _shippingService, _logger);

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
                            // Don't fail the whole transaction if email fails
                        }
                    }
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
                        order.Status = "cancelled";

                        var notes = new Dictionary<string, object?>();
                        if (order.Notes != null)
                            foreach (var p in order.Notes.RootElement.EnumerateObject())
                                notes[p.Name] = p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() : p.Value.GetRawText();
                        
                        notes["cancellation_reason"] = $"Payment failed or cancelled via PayOS Webhook (Code: {cmd.Webhook.Code}).";
                        order.Notes = JsonDocument.Parse(JsonSerializer.Serialize(notes));

                        // Restore stock
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

public class SyncPaymentCommandHandler : IRequestHandler<SyncPaymentCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly IPayOSService _payOS;
    private readonly IShippingService _shippingService;
    private readonly ILogger<SyncPaymentCommandHandler> _logger;

    public SyncPaymentCommandHandler(
        IApplicationDbContext context, 
        IPayOSService payOS, 
        IShippingService shippingService, 
        ILogger<SyncPaymentCommandHandler> logger)
    {
        _context = context;
        _payOS = payOS;
        _shippingService = shippingService;
        _logger = logger;
    }

    public async Task<bool> Handle(SyncPaymentCommand request, CancellationToken ct)
    {
        var targetString = request.OrderId.ToString();
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
                    // Fallback: might be stored as a string representation of an array
                    var parsed = JsonDocument.Parse(originalOrderIdsJson.GetString()!);
                    orderIds = parsed.RootElement.EnumerateArray().Select(e => e.GetGuid()).ToList();
                }

                var details = new Dictionary<string, object?>();
                foreach (var prop in payment.Details.RootElement.EnumerateObject())
                {
                    if (prop.Name == "order_ids")
                        details[prop.Name] = orderIds; // preserve as real array
                    else
                        details[prop.Name] = prop.Value.ValueKind == JsonValueKind.String ? prop.Value.GetString() : prop.Value.GetRawText();
                }
                
                if (details["status"]?.ToString() == "paid") return true; // already synced

                details["status"] = "paid";
                payment.Details = JsonDocument.Parse(JsonSerializer.Serialize(details));

                var orders = await _context.OrderHeaders
                    .Include(o => o.OrderItems)
                    .Where(o => orderIds.Contains(o.Id))
                    .ToListAsync(ct);
                
                foreach (var order in orders)
                {
                    if (order.Status == "pending")
                    {
                        order.Status = "confirmed";
                        order.ConfirmedAt = DateTime.UtcNow;

                        // Create GHN shipping order now that payment is confirmed
                        await GhnOrderHelper.TryCreateGhnOrderAsync(order, _shippingService, _logger);
                    }
                }
                
                await _context.SaveChangesAsync(ct);
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

/// <summary>
/// Shared helper for creating GHN shipping orders after payment confirmation.
/// Chain Store model: groups OrderItems by branch and creates N GHN shipments for 1 order.
/// </summary>
internal static class GhnOrderHelper
{
    internal static async Task TryCreateGhnOrderAsync(OrderHeader order, IShippingService shippingService, ILogger logger)
    {
        if (order.DeliveryAddress == null || order.OrderItems == null || order.OrderItems.Count == 0) return;

        // Skip if shipments already created
        if (order.Notes != null && order.Notes.RootElement.TryGetProperty("shipments", out _)) return;
        // Backward compat: also skip if old tracking_code exists
        if (order.Notes != null && order.Notes.RootElement.TryGetProperty("tracking_code", out _)) return;

        try
        {
            // Parse delivery address once
            var toName = order.DeliveryAddress.RootElement.TryGetProperty("recipient_name", out var n)
                ? (n.GetString() ?? "") : "";
            var toPhone = order.DeliveryAddress.RootElement.TryGetProperty("phone", out var p)
                ? (p.GetString() ?? "") : "";
            var toAddress = order.DeliveryAddress.RootElement.TryGetProperty("address_line_1", out var a)
                ? (a.GetString() ?? "") : "";
            var toDistrict = order.DeliveryAddress.RootElement.TryGetProperty("district_id", out var d)
                ? d.GetInt32() : 1454;
            var toWard = order.DeliveryAddress.RootElement.TryGetProperty("ward_code", out var w)
                ? (w.GetString() ?? "21211") : "21211";

            // Group OrderItems by BranchId (direct column on OrderItem)
            var itemsByBranch = order.OrderItems
                .GroupBy(oi => oi.BranchId)
                .ToList();

            var totalStr = order.Financials?.RootElement.TryGetProperty("total", out var t) == true
                ? (t.GetString() ?? "0") : "0";
            var orderTotal = (int)decimal.Parse(totalStr);

            var shipments = new List<object>();
            var shipmentIndex = 0;

            foreach (var branchGroup in itemsByBranch)
            {
                shipmentIndex++;
                var branchItems = branchGroup.ToList();

                var ghnItems = branchItems.Select(oi =>
                {
                    var itemName = "Decorative Plant";
                    if (oi.Snapshots != null && oi.Snapshots.RootElement.TryGetProperty("title_snapshot", out var ts))
                        itemName = ts.GetString() ?? itemName;
                    return new ShippingOrderItem
                    {
                        Name = itemName,
                        Quantity = oi.Quantity,
                        Weight = 1000
                    };
                }).ToList();

                // Split insurance value proportionally
                var branchInsurance = itemsByBranch.Count == 1 ? orderTotal :
                    (int)(orderTotal * ((decimal)branchItems.Sum(oi => oi.Quantity) / order.OrderItems.Sum(oi => oi.Quantity)));

                var clientOrderCode = itemsByBranch.Count == 1
                    ? (order.OrderCode ?? order.Id.ToString())
                    : $"{order.OrderCode}-S{shipmentIndex}";

                var ghnRes = await shippingService.CreateOrderAsync(new ShippingOrderRequest
                {
                    ToName = toName,
                    ToPhone = toPhone,
                    ToAddress = toAddress,
                    ToDistrictId = toDistrict,
                    ToWardCode = toWard,
                    Weight = ghnItems.Sum(i => i.Quantity) * 1000,
                    InsuranceValue = branchInsurance,
                    ClientOrderCode = clientOrderCode,
                    Items = ghnItems
                });

                if (ghnRes.Success && !string.IsNullOrEmpty(ghnRes.OrderCode))
                {
                    shipments.Add(new
                    {
                        branch_id = branchGroup.Key?.ToString(),
                        tracking_code = ghnRes.OrderCode,
                        carrier = "GHN",
                        items = branchItems.Select(oi =>
                        {
                            if (oi.Snapshots != null && oi.Snapshots.RootElement.TryGetProperty("title_snapshot", out var ts))
                                return ts.GetString() ?? "item";
                            return "item";
                        }).ToList()
                    });

                    logger.LogInformation("GHN shipment created for Order {OrderCode}, Branch {BranchId}: {TrackingCode}",
                        order.OrderCode, branchGroup.Key, ghnRes.OrderCode);
                }
            }

            // Save all tracking info to order.Notes
            if (shipments.Count > 0)
            {
                var notesObj = new Dictionary<string, object?>();
                if (order.Notes != null)
                {
                    foreach (var prop in order.Notes.RootElement.EnumerateObject())
                        notesObj[prop.Name] = prop.Value.ValueKind == JsonValueKind.String
                            ? prop.Value.GetString() : prop.Value.GetRawText();
                }
                notesObj["shipments"] = shipments;
                // Backward compat: set tracking_code to first shipment's code
                if (shipments.Count == 1)
                {
                    var first = (dynamic)shipments[0];
                    notesObj["tracking_code"] = first.tracking_code;
                    notesObj["carrier_name"] = "GHN";
                }
                order.Notes = JsonDocument.Parse(JsonSerializer.Serialize(notesObj));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create GHN shipments for {OrderCode}", order.OrderCode);
        }
    }
}
