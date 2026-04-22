using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using decorativeplant_be.Application.Common.DTOs.Email;
using decorativeplant_be.Application.Common.Options;
using decorativeplant_be.Application.Features.Commerce.Orders;
using decorativeplant_be.Application.Services;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Application.Common;

/// <summary>
/// Offline counter ship-to-customer: token in <see cref="OrderHeader.Notes"/> + email with confirm link.
/// Does not apply to online web checkout orders.
/// </summary>
public static class OfflineDeliveryDeliveredMailHelper
{
    public const string NotesTokenKey = "offline_delivered_confirm_token";
    public const string NotesExpiresKey = "offline_delivered_confirm_expires_utc";

    public static string CreateNewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    public static void MergeConfirmTokenIntoNotes(Dictionary<string, object?> notesDict, string token, DateTimeOffset expiresUtc)
    {
        notesDict[NotesTokenKey] = token;
        notesDict[NotesExpiresKey] = expiresUtc.ToString("o", CultureInfo.InvariantCulture);
    }

    /// <summary>Shallow copy of order notes into a mutable dictionary (same shape as <c>MergeNotes</c> start).</summary>
    public static Dictionary<string, object?> CloneNotesToDictionary(OrderHeader? order)
    {
        var dict = new Dictionary<string, object?>();
        if (order?.Notes == null) return dict;

        foreach (var p in order.Notes.RootElement.EnumerateObject())
        {
            if (p.Name == "status_history" && p.Value.ValueKind == JsonValueKind.Array)
            {
                var items = new List<object?>();
                foreach (var el in p.Value.EnumerateArray())
                    items.Add(JsonSerializer.Deserialize<object?>(el.GetRawText()));
                dict[p.Name] = items;
            }
            else
            {
                dict[p.Name] = p.Value.ValueKind switch
                {
                    JsonValueKind.String => p.Value.GetString(),
                    JsonValueKind.Number => p.Value.TryGetInt64(out var l) ? l : p.Value.GetDouble(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Null => null,
                    _ => JsonSerializer.Deserialize<object?>(p.Value.GetRawText()),
                };
            }
        }

        return dict;
    }

    public static bool TryReadConfirmToken(OrderHeader order, out string? token, out DateTimeOffset? expiresUtc)
    {
        token = null;
        expiresUtc = null;
        if (order.Notes == null) return false;
        var r = order.Notes.RootElement;
        if (r.TryGetProperty(NotesTokenKey, out var t) && t.ValueKind == JsonValueKind.String)
            token = t.GetString();
        if (r.TryGetProperty(NotesExpiresKey, out var e) && e.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(e.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
            expiresUtc = parsed;
        return !string.IsNullOrEmpty(token);
    }

    public static void RemoveConfirmTokenFromNotes(Dictionary<string, object?> notesDict)
    {
        notesDict.Remove(NotesTokenKey);
        notesDict.Remove(NotesExpiresKey);
    }

    public static bool TokensEqual(string provided, string stored)
    {
        try
        {
            var a = Convert.FromHexString(provided.Trim());
            var b = Convert.FromHexString(stored.Trim());
            return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
        }
        catch
        {
            return false;
        }
    }

    public static string BuildConfirmUrl(string baseUrl, Guid orderId, string token)
    {
        var root = (baseUrl ?? "").Trim().TrimEnd('/');
        var path = $"/confirm-offline-delivery?orderId={Uri.EscapeDataString(orderId.ToString())}&token={Uri.EscapeDataString(token)}";
        return string.IsNullOrEmpty(root) ? path : $"{root}{path}";
    }

    public static async Task TrySendDeliveredConfirmEmailAsync(
        OrderHeader order,
        string token,
        IOptions<CustomerPortalLinksOptions> portalOptions,
        IEmailService emailService,
        ILogger logger,
        CancellationToken ct = default)
    {
        var to = OrderCustomerNotificationHelper.GetDeliveryRecipientEmail(order);
        if (string.IsNullOrWhiteSpace(to))
        {
            logger.LogWarning(
                "OfflineDeliveryDeliveredMailHelper: No delivery recipient email for order {OrderCode}, skipping mail.",
                order.OrderCode);
            return;
        }

        var baseUrl = portalOptions.Value.BaseUrl?.Trim() ?? "";
        if (string.IsNullOrEmpty(baseUrl))
            logger.LogWarning(
                "OfflineDeliveryDeliveredMailHelper: Frontend BaseUrl is empty — confirm link may be broken for {OrderCode}.",
                order.OrderCode);

        var name = WebUtility.HtmlEncode(OrderCustomerNotificationHelper.GetDeliveryRecipientName(order) ?? "Quý khách");
        var code = WebUtility.HtmlEncode(order.OrderCode ?? order.Id.ToString());
        var confirmUrl = WebUtility.HtmlEncode(BuildConfirmUrl(baseUrl, order.Id, token));

        var bodyHtml =
            $"<div style=\"font-family:sans-serif;max-width:600px;margin:0 auto;color:#1f2937;\">" +
            $"<p>Xin chào <strong>{name}</strong>,</p>" +
            $"<p>Đơn hàng <strong>{code}</strong> đã được giao đến địa chỉ của bạn.</p>" +
            $"<p>Vui lòng bấm nút bên dưới để xác nhận bạn đã nhận hàng. Sau khi xác nhận, đơn sẽ chuyển sang <strong>hoàn tất</strong>.</p>" +
            $"<p style=\"margin:24px 0;\"><a href=\"{confirmUrl}\" style=\"display:inline-block;padding:12px 24px;background:#2d5f4d;color:#fff;text-decoration:none;border-radius:8px;font-weight:bold;\">Xác nhận đã nhận hàng</a></p>" +
            $"<p style=\"font-size:13px;color:#6b7280;\">Nếu nút không hoạt động, sao chép liên kết sau vào trình duyệt:<br/><span style=\"word-break:break-all;\">{confirmUrl}</span></p>" +
            $"</div>";

        var plain =
            $"Xin chao,\n\nDon hang {order.OrderCode} da duoc giao den ban.\n\n" +
            $"Xac nhan da nhan hang (mo lien ket trong trinh duyet):\n{BuildConfirmUrl(baseUrl, order.Id, token)}\n\n" +
            "Tran trong,\nDecorative Plant";

        try
        {
            await emailService.SendAsync(new EmailMessage
            {
                To = to,
                ToName = OrderCustomerNotificationHelper.GetDeliveryRecipientName(order),
                Subject = $"Đơn hàng {order.OrderCode} đã tới — vui lòng xác nhận",
                BodyPlainText = plain,
                BodyHtml = bodyHtml,
            }, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "OfflineDeliveryDeliveredMailHelper: failed to send delivered mail for {OrderCode}", order.OrderCode);
        }
    }

    /// <summary>After order is already in <see cref="OrderStatusMachine.Delivered"/>: merge token into notes and send mail.</summary>
    public static async Task TryIssueTokenAndNotifyForDeliveredOfflineOrderAsync(
        OrderHeader order,
        IOptions<CustomerPortalLinksOptions> portalOptions,
        IEmailService emailService,
        ILogger logger,
        CancellationToken ct = default)
    {
        if (!OrderCustomerNotificationHelper.IsOfflineDeliveryOrder(order)) return;
        if (!string.Equals(order.Status, OrderStatusMachine.Delivered, StringComparison.OrdinalIgnoreCase)) return;

        var token = CreateNewToken();
        var expires = DateTimeOffset.UtcNow.AddDays(30);
        var dict = CloneNotesToDictionary(order);
        MergeConfirmTokenIntoNotes(dict, token, expires);
        order.Notes = JsonDocument.Parse(JsonSerializer.Serialize(dict));

        await TrySendDeliveredConfirmEmailAsync(order, token, portalOptions, emailService, logger, ct);
    }
}
