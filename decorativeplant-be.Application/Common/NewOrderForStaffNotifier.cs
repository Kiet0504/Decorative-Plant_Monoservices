using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using decorativeplant_be.Application.Common.DTOs.Email;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Services;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Application.Common;

/// <summary>
/// Emails fulfillment_staff assigned to the order's branch when a new order is
/// ready to be picked/packed. Fires for:
///   - COD orders (CreateOrderHandler, auto-confirmed)
///   - Bank transfer orders (PayOS webhook, paid)
///
/// Failures are swallowed by the caller — never block the order flow on email.
/// </summary>
public static class NewOrderForStaffNotifier
{
    private static readonly string[] NotifyRoles = { "fulfillment_staff" };

    public static async Task NotifyAsync(
        OrderHeader order,
        IApplicationDbContext context,
        IEmailService emailService,
        ILogger logger,
        CancellationToken ct = default)
    {
        var branchId = order.OrderItems?.FirstOrDefault()?.BranchId;
        if (branchId == null)
        {
            logger.LogWarning("NewOrderForStaffNotifier: Order {OrderCode} has no branch, skipping.", order.OrderCode);
            return;
        }

        // Single query: staff + branch name in one round-trip. Select only the
        // fields we need so we don't accidentally pull down avatar blobs etc.
        var staffList = await (from sa in context.StaffAssignments
                               join u in context.UserAccounts on sa.StaffId equals u.Id
                               join b in context.Branches on sa.BranchId equals b.Id
                               where sa.BranchId == branchId.Value
                                  && NotifyRoles.Contains(u.Role)
                                  && u.IsActive
                                  && u.Email != null && u.Email != ""
                               select new { u.Email, u.DisplayName, u.Role, BranchName = b.Name })
                              .ToListAsync(ct);

        if (staffList.Count == 0)
        {
            logger.LogInformation("NewOrderForStaffNotifier: No eligible staff at branch {BranchId} for Order {OrderCode}.", branchId, order.OrderCode);
            return;
        }

        var total = order.Financials?.RootElement.TryGetProperty("total", out var t) == true
            ? t.GetString() ?? "0"
            : "0";
        var itemCount = order.OrderItems!.Sum(oi => oi.Quantity);
        var branchName = staffList[0].BranchName ?? "your branch";
        var subject = $"[New order] {order.OrderCode} — {branchName}";

        foreach (var s in staffList)
        {
            try
            {
                var greeting = string.IsNullOrWhiteSpace(s.DisplayName)
                    ? "Hello,"
                    : $"Hello {WebUtility.HtmlEncode(s.DisplayName)},";

                var bodyHtml =
                    $"<p>{greeting}</p>" +
                    $"<p>A new order is ready for fulfillment at <strong>{WebUtility.HtmlEncode(branchName)}</strong>.</p>" +
                    "<ul>" +
                    $"<li><strong>Order code:</strong> {WebUtility.HtmlEncode(order.OrderCode ?? order.Id.ToString())}</li>" +
                    $"<li><strong>Items:</strong> {itemCount}</li>" +
                    $"<li><strong>Total:</strong> {WebUtility.HtmlEncode(total)} VND</li>" +
                    $"<li><strong>Status:</strong> {WebUtility.HtmlEncode(order.Status ?? "confirmed")}</li>" +
                    "</ul>" +
                    "<p>Please check the staff dashboard to begin packing.</p>";

                var plain = $"New order {order.OrderCode} at {branchName}. Items: {itemCount}. Total: {total} VND. Status: {order.Status}. Please check the staff dashboard.";

                await emailService.SendAsync(new EmailMessage
                {
                    To = s.Email,
                    ToName = s.DisplayName,
                    Subject = subject,
                    BodyPlainText = plain,
                    BodyHtml = bodyHtml,
                }, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "NewOrderForStaffNotifier: Failed to email {Email} for Order {OrderCode}", s.Email, order.OrderCode);
            }
        }

        logger.LogInformation("NewOrderForStaffNotifier: Notified {Count} staff at branch {BranchId} for Order {OrderCode}.",
            staffList.Count, branchId, order.OrderCode);
    }
}
