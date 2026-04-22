using System.Text.Json;
using Microsoft.Extensions.Logging;
using decorativeplant_be.Application.Services;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Application.Common;

/// <summary>
/// Resolves recipient contact for customer-facing emails: <see cref="OrderHeader.Notes"/> <c>recipient_email</c>
/// (offline counter delivery) or <see cref="OrderHeader.DeliveryAddress"/> (online / legacy).
/// </summary>
public static class OrderCustomerNotificationHelper
{
    /// <summary>Staff offline ship-from-branch stores recipient email on <see cref="OrderHeader.Notes"/> (not on DeliveryAddress JSON for GHN hygiene).</summary>
    public const string NotesRecipientEmailKey = "recipient_email";

    public static string? GetDeliveryRecipientEmail(OrderHeader order)
    {
        if (order.Notes != null)
        {
            var n = order.Notes.RootElement;
            if (n.TryGetProperty(NotesRecipientEmailKey, out var ne) && ne.ValueKind == JsonValueKind.String)
            {
                var s = ne.GetString()?.Trim();
                if (!string.IsNullOrEmpty(s)) return s;
            }
        }

        if (order.DeliveryAddress == null) return null;
        var r = order.DeliveryAddress.RootElement;
        if (r.TryGetProperty("email", out var e) && e.ValueKind == JsonValueKind.String)
        {
            var s = e.GetString()?.Trim();
            if (!string.IsNullOrEmpty(s)) return s;
        }

        if (r.TryGetProperty("recipient_email", out var e2) && e2.ValueKind == JsonValueKind.String)
        {
            var s = e2.GetString()?.Trim();
            if (!string.IsNullOrEmpty(s)) return s;
        }

        return null;
    }

    public static string? GetDeliveryRecipientName(OrderHeader order)
    {
        if (order.DeliveryAddress == null) return null;
        var r = order.DeliveryAddress.RootElement;
        if (r.TryGetProperty("recipient_name", out var n) && n.ValueKind == JsonValueKind.String)
            return n.GetString()?.Trim();
        return null;
    }

    /// <summary>Walk-in / staff-created offline shipment: do not fall back to staff login email for OrderConfirmed.</summary>
    public static bool IsOfflineDeliveryOrder(OrderHeader order)
    {
        if (order.TypeInfo == null) return false;
        var r = order.TypeInfo.RootElement;
        var ot = r.TryGetProperty("order_type", out var otEl) ? otEl.GetString() : null;
        var fm = r.TryGetProperty("fulfillment_method", out var fmEl) ? fmEl.GetString() : null;
        return string.Equals(ot, "offline", StringComparison.OrdinalIgnoreCase)
               && string.Equals(fm, "delivery", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Sends OrderConfirmed: uses recipient email on delivery address when set; otherwise the
    /// account holder email for online orders — but not for offline delivery (avoid emailing staff).
    /// </summary>
    public static async Task TrySendOrderConfirmedEmailAsync(
        OrderHeader order,
        UserAccount? accountUser,
        IEmailTemplateService emailTemplateService,
        ILogger logger,
        CancellationToken ct = default)
    {
        var deliveryEmail = GetDeliveryRecipientEmail(order);
        var toEmail = !string.IsNullOrEmpty(deliveryEmail)
            ? deliveryEmail
            : (IsOfflineDeliveryOrder(order) ? null : accountUser?.Email);
        if (string.IsNullOrWhiteSpace(toEmail)) return;

        var customerName = !string.IsNullOrEmpty(deliveryEmail)
            ? (GetDeliveryRecipientName(order) ?? "Customer")
            : (accountUser?.DisplayName ?? "Customer");

        try
        {
            var total = "0";
            if (order.Financials != null)
                total = order.Financials.RootElement.TryGetProperty("total", out var t) ? t.GetString() ?? "0" : "0";

            var model = new Dictionary<string, string>
            {
                { "CustomerName", customerName },
                { "OrderCode", order.OrderCode ?? "N/A" },
                { "BranchName", "Decorative Plant Store" },
                { "Total", total }
            };

            await emailTemplateService.SendTemplateAsync(
                "OrderConfirmed",
                model,
                toEmail,
                $"Order Confirmed - {order.OrderCode}",
                customerName,
                ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send order confirmation email for {OrderCode}", order.OrderCode);
        }
    }
}
