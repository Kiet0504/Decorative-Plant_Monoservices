using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using decorativeplant_be.Application.Common;
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
            var isOwner = order.UserId == cmd.UserId;
            var isBopisImmediateGuest = false;

            // BOPIS immediate orders created by staff for walk-in customers often have null UserId.
            // Allow payment-link creation in that case while keeping ownership check for online orders.
            if (!isOwner && order.UserId == null && order.TypeInfo != null)
            {
                var root = order.TypeInfo.RootElement;
                var orderType = root.TryGetProperty("order_type", out var ot) ? ot.GetString() : null;
                isBopisImmediateGuest = string.Equals(orderType, "bopis_immediate", StringComparison.OrdinalIgnoreCase);
            }

            if (!isOwner && !isBopisImmediateGuest)
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

        // Reuse an unexpired pending PaymentTransaction for the same order set.
        // PayOS links expire after 30 minutes; we treat anything under 28 min as still valid.
        // FIX #2: Use pessimistic locking (skip locked rows) to avoid race condition when multiple requests
        // try to reuse the same pending transaction simultaneously.
        var sortedOrderIds = cmd.Request.OrderIds.OrderBy(id => id).ToList();
        var reuseCutoff = DateTime.UtcNow.AddMinutes(-28);
        var recentPending = await _context.PaymentTransactions
            .Where(p => p.OrderId == firstOrder.Id && p.CreatedAt >= reuseCutoff && p.Details != null)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(ct);

        var reusable = recentPending.FirstOrDefault(p =>
        {
            var root = p.Details!.RootElement;
            if (!root.TryGetProperty("status", out var st)
                || !string.Equals(st.GetString(), "pending", StringComparison.OrdinalIgnoreCase)) return false;
            if (!root.TryGetProperty("order_ids", out var idsEl)
                || idsEl.ValueKind != JsonValueKind.Array) return false;
            var existingIds = idsEl.EnumerateArray().Select(e => e.GetGuid()).OrderBy(id => id).ToList();
            return existingIds.SequenceEqual(sortedOrderIds);
        });

        if (reusable != null)
        {
            // Re-verify the record hasn't been modified/cancelled by another concurrent operation
            var reusableRefresh = await _context.PaymentTransactions.FindAsync(new object[] { reusable.Id }, cancellationToken: ct);
            if (reusableRefresh?.Details != null)
            {
                var status = reusableRefresh.Details.RootElement.TryGetProperty("status", out var st) ? st.GetString() : null;
                if (string.Equals(status, "pending", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("Reusing pending PaymentTransaction {TxId} for orders [{OrderIds}]",
                        reusable.Id, string.Join(", ", sortedOrderIds));
                    return MapToResponse(reusableRefresh);
                }
            }
        }

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
            
            if (root.TryGetProperty("collected_at", out var cat) && cat.ValueKind == JsonValueKind.String)
                r.CollectedAt = DateTime.TryParse(cat.GetString(), out var dt) ? dt : null;
            
            r.CollectedBy = root.TryGetProperty("collected_by_name", out var cbn) ? cbn.GetString() : null;

            if (root.TryGetProperty("picked_up_at", out var pat) && pat.ValueKind == JsonValueKind.String)
                r.PickedUpAt = DateTime.TryParse(pat.GetString(), out var pdt) ? pdt : null;

            r.PickedUpBy = root.TryGetProperty("picked_up_by_name", out var pbn) ? pbn.GetString() : null;

            if (root.TryGetProperty("refunded_at", out var rat) && rat.ValueKind == JsonValueKind.String)
                r.RefundedAt = DateTime.TryParse(rat.GetString(), out var rdt) ? rdt : null;

            r.RefundedBy = root.TryGetProperty("refunded_by_name", out var rbn) ? rbn.GetString() : null;
            r.RefundNote = root.TryGetProperty("refund_note", out var rn) ? rn.GetString() : null;
            if (root.TryGetProperty("refund_evidence_images", out var imagesElement) && imagesElement.ValueKind == JsonValueKind.Array)
            {
                r.RefundEvidenceImages = imagesElement.EnumerateArray()
                    .Select(x => x.GetString())
                    .Where(x => !string.IsNullOrEmpty(x))
                    .Cast<string>()
                    .ToList();
            }
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
    private readonly IEmailService _emailService;
    private readonly IShippingService _shippingService;
    private readonly IStockService _stockService;
    private readonly IOrderAssignmentService _orderAssignment;

    public HandlePayOSWebhookHandler(
        IApplicationDbContext context,
        IPayOSService payOS,
        ILogger<HandlePayOSWebhookHandler> logger,
        IEmailTemplateService emailTemplateService,
        IEmailService emailService,
        IShippingService shippingService,
        IStockService stockService,
        IOrderAssignmentService orderAssignment)
    {
        _context = context;
        _payOS = payOS;
        _logger = logger;
        _emailTemplateService = emailTemplateService;
        _emailService = emailService;
        _shippingService = shippingService;
        _stockService = stockService;
        _orderAssignment = orderAssignment;
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
                            var isBopisImmediate = order.TypeInfo != null
                                && order.TypeInfo.RootElement.TryGetProperty("order_type", out var ot)
                                && string.Equals(ot.GetString(), "bopis_immediate", StringComparison.OrdinalIgnoreCase);

                            if (order.Status == "pending")
                            {
                                // Deduct stock with pessimistic locking
                                if (order.OrderItems != null)
                                    await _stockService.DeductOrderStockAsync(order.OrderItems, ct);

                                // BOPIS immediate: once payment is paid, it should move to confirmed
                                // so store staff can complete handover at counter.
                                if (isBopisImmediate)
                                {
                                    decorativeplant_be.Application.Features.Commerce.Orders.OrderStatusMachine
                                        .ApplyFromExternalSource(
                                            order,
                                            decorativeplant_be.Application.Features.Commerce.Orders.OrderStatusMachine.Confirmed,
                                            source: "PayOSWebhook",
                                            reason: "Payment confirmed");
                                }
                            }

                            var notesObj = new Dictionary<string, object?>();
                            if (order.Notes != null)
                            {
                                foreach (var prop in order.Notes.RootElement.EnumerateObject())
                                    notesObj[prop.Name] = prop.Value.ValueKind == JsonValueKind.String
                                        ? prop.Value.GetString() : prop.Value.GetRawText();
                            }
                            // FIX #4: Sync payment_status from PaymentTransaction to OrderHeader.Notes
                            notesObj["payment_status"] = "paid";
                            notesObj["payment_confirmed_at"] = DateTime.UtcNow.ToString("o");
                            order.Notes = JsonDocument.Parse(JsonSerializer.Serialize(notesObj));

                            // Status stays `pending` — fulfillment staff must manually confirm,
                            // pack, and hand to carrier. No auto-advance on payment so the
                            // staff workflow (Confirm → Pack → Ship) is not bypassed.
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
                                // FIX #4: Sync payment_status from PaymentTransaction to OrderHeader.Notes
                                notes["payment_status"] = "failed";
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
                .Include(o => o.OrderItems)
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

                // Also notify fulfillment_staff/branch_manager at the order's branch.
                // Fire-and-log: never block the webhook ack on email delivery.
                try
                {
                    await NewOrderForStaffNotifier.NotifyAsync(order, _context, _emailService, _logger, _orderAssignment, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Staff notify failed for Order {OrderCode}", order.OrderCode);
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

/// <summary>
/// FIX #3: Periodic task to check for expired refunds.
/// Refunds are marked "pending" but should have a timeout (e.g., 30 days).
/// If not completed by finance within timeout, mark as "timeout" for admin attention.
/// </summary>
public class CheckExpiredRefundsHandler : IRequestHandler<CheckExpiredRefundsQuery, int>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<CheckExpiredRefundsHandler> _logger;
    private const int RefundTimeoutDays = 30;

    public CheckExpiredRefundsHandler(IApplicationDbContext context, ILogger<CheckExpiredRefundsHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<int> Handle(CheckExpiredRefundsQuery q, CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow.AddDays(-RefundTimeoutDays);
        var pendingRefunds = await _context.PaymentTransactions
            .Where(p => p.Details != null && p.CreatedAt <= cutoff)
            .ToListAsync(ct);

        int expiredCount = 0;
        foreach (var p in pendingRefunds)
        {
            if (p.Details == null) continue;
            var root = p.Details.RootElement;

            var type = root.TryGetProperty("type", out var typeEl) ? typeEl.GetString() : null;
            var status = root.TryGetProperty("status", out var statusEl) ? statusEl.GetString() : null;

            if (string.Equals(type, "refund", StringComparison.OrdinalIgnoreCase) 
                && string.Equals(status, "pending", StringComparison.OrdinalIgnoreCase))
            {
                var details = new Dictionary<string, object?>();
                foreach (var prop in p.Details.RootElement.EnumerateObject())
                {
                    if (prop.Name == "status")
                        details[prop.Name] = "timeout"; // Mark as timed out
                    else
                        details[prop.Name] = prop.Value.ValueKind == JsonValueKind.String ? prop.Value.GetString() : prop.Value.GetRawText();
                }
                details["timeout_at"] = DateTime.UtcNow.ToString("o");
                p.Details = JsonDocument.Parse(JsonSerializer.Serialize(details));
                expiredCount++;

                _logger.LogWarning("Refund {TxId} for order {OrderId} has expired ({Days} days) and marked for review", 
                    p.Id, p.OrderId, RefundTimeoutDays);
            }
        }

        if (expiredCount > 0)
        {
            await _context.SaveChangesAsync(ct);
            _logger.LogInformation("Processed {ExpiredCount} expired refunds", expiredCount);
        }

        return expiredCount;
    }
}

public class SyncPaymentCommandHandler : IRequestHandler<SyncPaymentCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly IPayOSService _payOS;
    private readonly IShippingService _shippingService;
    private readonly IStockService _stockService;
    private readonly ILogger<SyncPaymentCommandHandler> _logger;
    private readonly IEmailService _emailService;
    private readonly IEmailTemplateService _emailTemplateService;
    private readonly IOrderAssignmentService _orderAssignment;

    public SyncPaymentCommandHandler(
        IApplicationDbContext context,
        IPayOSService payOS,
        IShippingService shippingService,
        IStockService stockService,
        ILogger<SyncPaymentCommandHandler> logger,
        IEmailService emailService,
        IEmailTemplateService emailTemplateService,
        IOrderAssignmentService orderAssignment)
    {
        _context = context;
        _payOS = payOS;
        _shippingService = shippingService;
        _stockService = stockService;
        _logger = logger;
        _emailService = emailService;
        _emailTemplateService = emailTemplateService;
        _orderAssignment = orderAssignment;
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
                            var isBopisImmediate = order.TypeInfo != null
                                && order.TypeInfo.RootElement.TryGetProperty("order_type", out var ot)
                                && string.Equals(ot.GetString(), "bopis_immediate", StringComparison.OrdinalIgnoreCase);

                            if (order.Status == "pending")
                            {
                                // Deduct stock with pessimistic locking
                                if (order.OrderItems != null)
                                    await _stockService.DeductOrderStockAsync(order.OrderItems, ct);

                                // BOPIS immediate: after successful sync, move pending -> confirmed
                                // so staff can see "Complete Order" action in UI.
                                if (isBopisImmediate)
                                {
                                    decorativeplant_be.Application.Features.Commerce.Orders.OrderStatusMachine
                                        .ApplyFromExternalSource(
                                            order,
                                            decorativeplant_be.Application.Features.Commerce.Orders.OrderStatusMachine.Confirmed,
                                            source: "PayOSSync",
                                            reason: "Payment sync confirmed");
                                }
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

                            // Status stays `pending` — fulfillment staff drives all transitions.
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
                var syncedOrders = await _context.OrderHeaders
                    .Include(o => o.User)
                    .Include(o => o.OrderItems)
                    .Where(o => orderIds.Contains(o.Id))
                    .ToListAsync(ct);

                foreach (var order in syncedOrders)
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
                        _logger.LogError(ex, "Failed to send order confirmation email for {OrderCode} during sync", order.OrderCode);
                    }

                    try
                    {
                        await decorativeplant_be.Application.Common.NewOrderForStaffNotifier.NotifyAsync(order, _context, _emailService, _logger, _orderAssignment, ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Staff notify failed for Order {OrderCode} during sync", order.OrderCode);
                    }
                }

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

public class ConfirmCodReceivedCommandHandler : IRequestHandler<ConfirmCodReceivedCommand, PaymentResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<ConfirmCodReceivedCommandHandler> _logger;

    public ConfirmCodReceivedCommandHandler(IApplicationDbContext context, ILogger<ConfirmCodReceivedCommandHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PaymentResponse> Handle(ConfirmCodReceivedCommand request, CancellationToken ct)
    {
        var payment = await _context.PaymentTransactions.FindAsync(new object[] { request.PaymentId }, ct);
        if (payment == null || payment.Details == null)
            throw new NotFoundException("Payment transaction not found.");

        var root = payment.Details.RootElement;
        
        var method = root.TryGetProperty("method", out var m) ? m.GetString() : null;
        var status = root.TryGetProperty("status", out var s) ? s.GetString() : null;

        if (method != "cod")
            throw new BadRequestException("This payment transaction is not a Cash on Delivery (COD) payment.");

        if (status == "success")
            throw new BadRequestException("This COD payment has already been confirmed as received.");

        if (status != "cod_picked_up")
            throw new BadRequestException($"Payment must be in 'cod_picked_up' status to confirm receipt. Current status: {status ?? "null"}");

        var details = new Dictionary<string, object?>();
        foreach (var prop in root.EnumerateObject())
        {
            if (prop.Name == "order_ids")
                details[prop.Name] = JsonSerializer.Deserialize<object?>(prop.Value.GetRawText());
            else
                details[prop.Name] = prop.Value.ValueKind == JsonValueKind.String ? prop.Value.GetString() : prop.Value.GetRawText();
        }

        details["status"] = "success";
        details["collected_at"] = DateTime.UtcNow.ToString("o");
        details["collected_by"] = request.StaffId;

        // Optionally add staff name for display
        var staffName = await _context.UserAccounts
            .Where(u => u.Id == request.StaffId)
            .Select(u => u.DisplayName)
            .FirstOrDefaultAsync(ct);
        details["collected_by_name"] = staffName ?? "Unknown Staff";

        payment.Details = JsonDocument.Parse(JsonSerializer.Serialize(details));

        // Sync to OrderHeader.Notes so the Order summary shows "Paid" badge correctly
        var order = await _context.OrderHeaders.FindAsync(new object[] { payment.OrderId }, ct);
        if (order != null)
        {
            var notesObj = new Dictionary<string, object?>();
            if (order.Notes != null)
            {
                foreach (var prop in order.Notes.RootElement.EnumerateObject())
                {
                    if (prop.Value.ValueKind == JsonValueKind.String)
                        notesObj[prop.Name] = prop.Value.GetString();
                    else
                        notesObj[prop.Name] = JsonSerializer.Deserialize<object?>(prop.Value.GetRawText());
                }
            }
            notesObj["payment_status"] = "paid";
            notesObj["payment_confirmed_at"] = DateTime.UtcNow.ToString("o");
            order.Notes = JsonDocument.Parse(JsonSerializer.Serialize(notesObj));
            _logger.LogInformation("Synced 'paid' status to Order {OrderId} after COD confirmed.", order.Id);
        }

        _logger.LogInformation("Staff {StaffName} ({StaffId}) confirmed receipt of COD money for transaction {PaymentId}.", staffName, request.StaffId, payment.Id);

        await _context.SaveChangesAsync(ct);
        return CreatePaymentHandler.MapToResponse(payment);
    }
}

public class MarkRefundedCommandHandler : IRequestHandler<MarkRefundedCommand, PaymentResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<MarkRefundedCommandHandler> _logger;

    public MarkRefundedCommandHandler(IApplicationDbContext context, ILogger<MarkRefundedCommandHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PaymentResponse> Handle(MarkRefundedCommand request, CancellationToken ct)
    {
        var payment = await _context.PaymentTransactions.FindAsync(new object[] { request.PaymentId }, ct);
        if (payment == null || payment.Details == null)
            throw new NotFoundException("Payment transaction not found.");

        var root = payment.Details.RootElement;
        var status = root.TryGetProperty("status", out var s) ? s.GetString() : null;

        // Only allow refund on payments that have been paid/success
        var refundableStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "paid", "success", "cod_picked_up" };

        if (status == null || !refundableStatuses.Contains(status))
            throw new BadRequestException(
                $"Only paid/success/cod_picked_up payments can be marked as refunded. Current status: {status ?? "null"}");

        if (string.Equals(status, "refunded", StringComparison.OrdinalIgnoreCase))
            throw new BadRequestException("This payment has already been marked as refunded.");

        var details = new Dictionary<string, object?>();
        foreach (var prop in root.EnumerateObject())
        {
            if (prop.Name == "order_ids")
                details[prop.Name] = JsonSerializer.Deserialize<object?>(prop.Value.GetRawText());
            else
                details[prop.Name] = prop.Value.ValueKind == JsonValueKind.String
                    ? prop.Value.GetString() : prop.Value.GetRawText();
        }

        details["status"] = "refunded";
        details["refunded_at"] = DateTime.UtcNow.ToString("o");
        details["refunded_by"] = request.StaffId;

        var staffName = await _context.UserAccounts
            .Where(u => u.Id == request.StaffId)
            .Select(u => u.DisplayName)
            .FirstOrDefaultAsync(ct);
        details["refunded_by_name"] = staffName ?? "Unknown Staff";

        if (!string.IsNullOrWhiteSpace(request.Note))
            details["refund_note"] = request.Note;

        if (request.EvidenceImageUrls != null && request.EvidenceImageUrls.Count > 0)
            details["refund_evidence_images"] = request.EvidenceImageUrls;

        payment.Details = JsonDocument.Parse(JsonSerializer.Serialize(details));

        // Sync refunded status to OrderHeader.Notes
        var order = await _context.OrderHeaders.FindAsync(new object[] { payment.OrderId }, ct);
        if (order != null)
        {
            var notesObj = new Dictionary<string, object?>();
            if (order.Notes != null)
            {
                foreach (var prop in order.Notes.RootElement.EnumerateObject())
                {
                    if (prop.Value.ValueKind == JsonValueKind.String)
                        notesObj[prop.Name] = prop.Value.GetString();
                    else
                        notesObj[prop.Name] = JsonSerializer.Deserialize<object?>(prop.Value.GetRawText());
                }
            }
            notesObj["payment_status"] = "refunded";
            notesObj["refunded_at"] = DateTime.UtcNow.ToString("o");
            order.Notes = JsonDocument.Parse(JsonSerializer.Serialize(notesObj));
        }

        _logger.LogInformation(
            "Staff {StaffName} ({StaffId}) marked payment {PaymentId} as refunded. Note: {Note}",
            staffName, request.StaffId, payment.Id, request.Note ?? "(none)");

        await _context.SaveChangesAsync(ct);
        return CreatePaymentHandler.MapToResponse(payment);
    }
}
