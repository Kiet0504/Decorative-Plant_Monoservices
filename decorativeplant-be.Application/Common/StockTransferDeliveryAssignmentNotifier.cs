using System.Net;
using decorativeplant_be.Application.Common.DTOs.Email;
using decorativeplant_be.Application.Common.Options;
using decorativeplant_be.Application.Services;
using Microsoft.Extensions.Logging;

namespace decorativeplant_be.Application.Common;

/// <summary>Inform fulfillment staff they were assigned an inter-branch stock transfer shipment.</summary>
public static class StockTransferDeliveryAssignmentNotifier
{
    public static async Task TryNotifyAssignedFulfillmentStaffAsync(
        IEmailService emailService,
        ILogger logger,
        CustomerPortalLinksOptions portal,
        IReadOnlyList<(string Email, string? DisplayName)> recipients,
        string transferCode,
        string? speciesName,
        string? toBranchName,
        int quantity,
        CancellationToken cancellationToken = default)
    {
        if (recipients.Count == 0) return;

        var baseUrl = portal.BaseUrl?.TrimEnd('/') ?? "";
        var logisticsUrl =
            string.IsNullOrWhiteSpace(baseUrl)
                ? string.Empty
                : $"{baseUrl}/fulfillment-staff/logistics";

        var plant = string.IsNullOrWhiteSpace(speciesName) ? "Stock" : speciesName;
        var dest = string.IsNullOrWhiteSpace(toBranchName) ? "the destination branch" : toBranchName;
        foreach (var (email, displayName) in recipients)
        {
            var greeting = string.IsNullOrWhiteSpace(displayName) ? "Hello," : $"Hello {WebUtility.HtmlEncode(displayName)},";
            var logisticsButton = string.IsNullOrWhiteSpace(logisticsUrl)
                ? "<p>Log in to the staff portal → Fulfillment → Branch logistics to confirm dispatch.</p>"
                : $"<p><a href=\"{WebUtility.HtmlEncode(logisticsUrl)}\">Open branch logistics</a> to confirm dispatch when ready.</p>";

            var bodyHtml =
                $"<p>{greeting}</p>" +
                $"<p>You are assigned as the dispatcher for inter-branch transfer " +
                $"<strong>{WebUtility.HtmlEncode(transferCode)}</strong> ({WebUtility.HtmlEncode(plant)}, {quantity} units) " +
                $"to <strong>{WebUtility.HtmlEncode(dest)}</strong>. Another colleague should handle packing alongside this shipment.</p>" +
                logisticsButton +
                "<p>If you did not expect this email, contact your branch manager.</p>";

            var plain =
                $"{(string.IsNullOrWhiteSpace(displayName) ? "Hello," : $"Hello {displayName},")} " +
                $"You are assigned to dispatch transfer {transferCode} ({plant}, {quantity} units → {dest}).";
            if (!string.IsNullOrWhiteSpace(logisticsUrl))
                plain += $" Open logistics: {logisticsUrl}";

            try
            {
                await emailService.SendAsync(
                    new EmailMessage
                    {
                        To = email,
                        ToName = displayName,
                        Subject = $"Decorative Plant — assigned transfer delivery ({transferCode})",
                        BodyPlainText = plain,
                        BodyHtml = bodyHtml,
                    },
                    cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "StockTransferDeliveryAssignment: failed to notify {Email} for {Code}",
                    email, transferCode);
            }
        }
    }
}
