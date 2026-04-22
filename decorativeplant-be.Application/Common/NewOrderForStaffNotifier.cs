using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using decorativeplant_be.Application.Common.DTOs.Email;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Services;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Application.Common;

/// <summary>
/// Assigns a fulfillment_staff to the order (workload-based) for online home-delivery and for
/// offline counter ship-to-customer orders (separate assignment entry in the service, same algorithm).
/// In-store pickup and non-delivery offline flows do not notify fulfillment here.
/// Falls back to broadcast when no one has capacity
/// (queue scenario). Fires for:
///   - COD orders (CreateOrderHandler, auto-confirmed)
///   - Bank transfer orders (PayOS webhook, paid)
///
/// Failures are swallowed by the caller — never block the order flow.
/// </summary>
public static class NewOrderForStaffNotifier
{
    private static readonly string[] NotifyRoles = { "fulfillment_staff" };

    public static async Task NotifyAsync(
        OrderHeader order,
        IApplicationDbContext context,
        IEmailService emailService,
        ILogger logger,
        IOrderAssignmentService assignmentService,
        CancellationToken ct = default)
    {
        var branchId = order.OrderItems?.FirstOrDefault()?.BranchId;
        if (branchId == null)
        {
            logger.LogWarning("NewOrderForStaffNotifier: Order {OrderCode} has no branch, skipping.", order.OrderCode);
            return;
        }

        // Pickup at branch / web BOPIS: no fulfillment assignment or fulfillment emails.
        if (OrderTypeInfoHelper.IsPickupAtBranchOrder(order))
        {
            logger.LogInformation(
                "NewOrderForStaffNotifier: Skipping fulfillment staff notify (in-store pickup) for {OrderCode}.",
                order.OrderCode);
            return;
        }

        // Offline deposit BOPIS / transfer flows — not warehouse ship-to-customer.
        if (OrderTypeInfoHelper.IsOfflineChannelOrder(order) && !OrderTypeInfoHelper.IsDeliveryFulfillment(order))
        {
            logger.LogInformation(
                "NewOrderForStaffNotifier: Skipping fulfillment staff notify (non-delivery offline workflow) for {OrderCode}.",
                order.OrderCode);
            return;
        }

        // Try workload-based assignment first
        UserAccount? assignedStaff = null;
        try
        {
            assignedStaff = await assignmentService.TryAssignAsync(order, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "NewOrderForStaffNotifier: Assignment failed for Order {OrderCode}, falling back to broadcast.", order.OrderCode);
        }

        var branchName = await context.Branches
            .Where(b => b.Id == branchId.Value)
            .Select(b => b.Name)
            .FirstOrDefaultAsync(ct) ?? "your branch";

        var total = order.Financials?.RootElement.TryGetProperty("total", out var t) == true
            ? t.GetString() ?? "0"
            : "0";
        var itemCount = order.OrderItems!.Sum(oi => oi.Quantity);

        if (assignedStaff != null)
        {
            // Notify only the assigned staff
            await SendEmailAsync(emailService, logger, assignedStaff.Email!, assignedStaff.DisplayName,
                order, branchName, itemCount, total, assigned: true, ct);

            logger.LogInformation(
                "NewOrderForStaffNotifier: Notified assigned staff {StaffId} for Order {OrderCode}.",
                assignedStaff.Id, order.OrderCode);
        }
        else
        {
            // Queue scenario — broadcast to all staff so no order is silently lost
            var staffList = await (
                from sa in context.StaffAssignments
                join u in context.UserAccounts on sa.StaffId equals u.Id
                where sa.BranchId == branchId.Value
                   && NotifyRoles.Contains(u.Role)
                   && u.IsActive
                   && u.Email != null && u.Email != ""
                select new { u.Email, u.DisplayName }
            ).ToListAsync(ct);

            if (staffList.Count == 0)
            {
                logger.LogInformation(
                    "NewOrderForStaffNotifier: No eligible staff at branch {BranchId} for Order {OrderCode}.",
                    branchId, order.OrderCode);
                return;
            }

            foreach (var s in staffList)
            {
                await SendEmailAsync(emailService, logger, s.Email!, s.DisplayName,
                    order, branchName, itemCount, total, assigned: false, ct);
            }

            logger.LogInformation(
                "NewOrderForStaffNotifier: All-staff broadcast ({Count}) for queued Order {OrderCode} at branch {BranchId}.",
                staffList.Count, order.OrderCode, branchId);
        }
    }

    private static async Task SendEmailAsync(
        IEmailService emailService,
        ILogger logger,
        string email,
        string? displayName,
        OrderHeader order,
        string branchName,
        int itemCount,
        string total,
        bool assigned,
        CancellationToken ct)
    {
        try
        {
            var greeting = string.IsNullOrWhiteSpace(displayName)
                ? "Hello,"
                : $"Hello <strong>{WebUtility.HtmlEncode(displayName)}</strong>,";

            var assignmentNote = assigned
                ? "<p style='color:#2d5f4d;font-weight:bold;'>✅ This order has been assigned to you. Please begin packing as soon as possible.</p>"
                : "<p style='color:#b45309;font-weight:bold;'>⏳ All staff are currently at capacity — this order is in the queue. Please pick it up when you have a free slot.</p>";

            // Extract delivery address
            string? recipientName = null, phone = null, addressLine = null, city = null, recipientEmail = null;
            if (order.DeliveryAddress != null)
            {
                var addr = order.DeliveryAddress.RootElement;
                recipientName = addr.TryGetProperty("recipient_name", out var rn) ? rn.GetString() : null;
                phone        = addr.TryGetProperty("phone", out var ph) ? ph.GetString() : null;
                addressLine  = addr.TryGetProperty("address_line_1", out var a1) ? a1.GetString() : null;
                city         = addr.TryGetProperty("city", out var c) ? c.GetString() : null;
                recipientEmail = addr.TryGetProperty("email", out var em) ? em.GetString() : null;
                if (string.IsNullOrWhiteSpace(recipientEmail) && order.Notes != null
                    && order.Notes.RootElement.TryGetProperty(OrderCustomerNotificationHelper.NotesRecipientEmailKey, out var nre)
                    && nre.ValueKind == JsonValueKind.String)
                    recipientEmail = nre.GetString();
            }

            // Extract fulfillment method & payment method
            string? fulfillmentMethod = null, paymentMethod = null;
            if (order.TypeInfo != null)
            {
                var ti = order.TypeInfo.RootElement;
                fulfillmentMethod = ti.TryGetProperty("fulfillment_method", out var fm) ? fm.GetString() : null;
                paymentMethod     = ti.TryGetProperty("payment_method", out var pm) ? pm.GetString() : null;
            }

            // Build item rows
            var itemRows = new System.Text.StringBuilder();
            if (order.OrderItems != null)
            {
                foreach (var oi in order.OrderItems)
                {
                    string? title = null, unitPrice = null;
                    if (oi.Snapshots != null)
                        title = oi.Snapshots.RootElement.TryGetProperty("title_snapshot", out var ts) ? ts.GetString() : null;
                    if (oi.Pricing != null)
                        unitPrice = oi.Pricing.RootElement.TryGetProperty("unit_price", out var up) ? up.GetString() : null;

                    itemRows.Append(
                        $"<tr>" +
                        $"<td style='padding:4px 8px;border-bottom:1px solid #e5e7eb;'>{WebUtility.HtmlEncode(title ?? oi.ListingId?.ToString() ?? "—")}</td>" +
                        $"<td style='padding:4px 8px;border-bottom:1px solid #e5e7eb;text-align:center;'>{oi.Quantity}</td>" +
                        $"<td style='padding:4px 8px;border-bottom:1px solid #e5e7eb;text-align:right;'>{WebUtility.HtmlEncode(unitPrice ?? "—")} VND</td>" +
                        $"</tr>");
                }
            }

            var deliverySection = recipientName != null
                ? $"<tr><th style='text-align:left;padding:4px 8px;color:#6b7280;font-weight:normal;'>Recipient</th>" +
                  $"<td style='padding:4px 8px;'>{WebUtility.HtmlEncode(recipientName)}" +
                  (phone != null ? $" · {WebUtility.HtmlEncode(phone)}" : "") + "</td></tr>" +
                  (!string.IsNullOrEmpty(recipientEmail)
                      ? $"<tr><th style='text-align:left;padding:4px 8px;color:#6b7280;font-weight:normal;'>Recipient email</th>" +
                        $"<td style='padding:4px 8px;'>{WebUtility.HtmlEncode(recipientEmail)}</td></tr>"
                      : "") +
                  $"<tr><th style='text-align:left;padding:4px 8px;color:#6b7280;font-weight:normal;'>Address</th>" +
                  $"<td style='padding:4px 8px;'>{WebUtility.HtmlEncode(addressLine ?? "—")}" +
                  (city != null ? $", {WebUtility.HtmlEncode(city)}" : "") + "</td></tr>"
                : "";

            var bodyHtml =
                $"<div style='font-family:sans-serif;max-width:600px;margin:0 auto;color:#1f2937;'>" +
                $"<p>{greeting}</p>" +
                $"<p>A new order is ready for fulfillment at <strong>{WebUtility.HtmlEncode(branchName)}</strong>.</p>" +
                assignmentNote +

                // Order summary table
                $"<table style='width:100%;border-collapse:collapse;margin:12px 0;'>" +
                $"<tr><th style='text-align:left;padding:4px 8px;color:#6b7280;font-weight:normal;'>Order code</th>" +
                $"<td style='padding:4px 8px;font-weight:bold;'>{WebUtility.HtmlEncode(order.OrderCode ?? order.Id.ToString())}</td></tr>" +
                $"<tr><th style='text-align:left;padding:4px 8px;color:#6b7280;font-weight:normal;'>Status</th>" +
                $"<td style='padding:4px 8px;'>{WebUtility.HtmlEncode(order.Status ?? "confirmed")}</td></tr>" +
                $"<tr><th style='text-align:left;padding:4px 8px;color:#6b7280;font-weight:normal;'>Fulfillment</th>" +
                $"<td style='padding:4px 8px;'>{WebUtility.HtmlEncode(fulfillmentMethod ?? "delivery")}</td></tr>" +
                $"<tr><th style='text-align:left;padding:4px 8px;color:#6b7280;font-weight:normal;'>Payment</th>" +
                $"<td style='padding:4px 8px;'>{WebUtility.HtmlEncode(paymentMethod ?? "—")}</td></tr>" +
                deliverySection +
                $"<tr><th style='text-align:left;padding:4px 8px;color:#6b7280;font-weight:normal;'>Total</th>" +
                $"<td style='padding:4px 8px;font-weight:bold;color:#2d5f4d;'>{WebUtility.HtmlEncode(total)} VND</td></tr>" +
                $"</table>" +

                // Item list
                (itemRows.Length > 0
                    ? $"<p style='margin-top:16px;font-weight:bold;'>Order items ({itemCount}):</p>" +
                      $"<table style='width:100%;border-collapse:collapse;font-size:14px;'>" +
                      $"<thead><tr style='background:#f3f4f6;'>" +
                      $"<th style='padding:4px 8px;text-align:left;'>Product</th>" +
                      $"<th style='padding:4px 8px;text-align:center;'>Qty</th>" +
                      $"<th style='padding:4px 8px;text-align:right;'>Unit price</th>" +
                      $"</tr></thead><tbody>{itemRows}</tbody></table>"
                    : "") +

                $"<p style='margin-top:20px;'>Please check the staff dashboard to begin packing.</p>" +
                $"</div>";

            var plain =
                $"Hi {displayName ?? "staff"},\n\n" +
                $"New order {order.OrderCode} at {branchName}.\n" +
                (assigned ? "This order is assigned to you.\n" : "This order is in the queue.\n") +
                (recipientName != null
                    ? $"Recipient: {recipientName} {phone}\n" +
                      (!string.IsNullOrEmpty(recipientEmail) ? $"Recipient email: {recipientEmail}\n" : "") +
                      $"Address: {addressLine}, {city}\n"
                    : "") +
                $"Items: {itemCount} | Total: {total} VND | Payment: {paymentMethod ?? "—"}\n\n" +
                "Please log in to the staff dashboard to begin packing.";

            var subject = assigned
                ? $"[Assigned to you] Order {order.OrderCode} — {branchName}"
                : $"[Queued order] Order {order.OrderCode} — {branchName}";

            await emailService.SendAsync(new EmailMessage
            {
                To = email,
                ToName = displayName,
                Subject = subject,
                BodyPlainText = plain,
                BodyHtml = bodyHtml,
            }, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "NewOrderForStaffNotifier: Failed to email {Email} for Order {OrderCode}", email, order.OrderCode);
        }
    }
}
