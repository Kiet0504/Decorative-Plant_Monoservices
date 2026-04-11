using System.Net;
using decorativeplant_be.Application.Common.DTOs.Email;
using decorativeplant_be.Application.Services;

namespace decorativeplant_be.Application.Common;

public static class StaffAssignmentEmailNotifier
{
    public static async Task SendStaffAssignedAsync(
        IEmailService emailService,
        string toEmail,
        string? displayName,
        string branchName,
        string roleCanonical,
        string? temporaryPasswordPlaintext,
        CancellationToken cancellationToken = default)
    {
        var roleLabel = RoleLabel(roleCanonical);
        var greeting = string.IsNullOrWhiteSpace(displayName) ? "Hello," : $"Hello {WebUtility.HtmlEncode(displayName)},";

        var pwdHtml = string.IsNullOrWhiteSpace(temporaryPasswordPlaintext)
            ? string.Empty
            : $"<p><strong>Temporary password:</strong> {WebUtility.HtmlEncode(temporaryPasswordPlaintext)}</p>" +
              "<p>Please sign in and change your password as soon as possible.</p>";

        var bodyHtml =
            $"<p>{greeting}</p>" +
            $"<p>You have been assigned as <strong>{WebUtility.HtmlEncode(roleLabel)}</strong> " +
            $"for branch <strong>{WebUtility.HtmlEncode(branchName)}</strong>.</p>" +
            pwdHtml +
            "<p>If you did not expect this email, contact support.</p>";

        var plain = string.IsNullOrWhiteSpace(displayName)
            ? "Hello,"
            : $"Hello {displayName},";
        plain += $" You have been assigned as {roleLabel} for branch {branchName}.";
        if (!string.IsNullOrWhiteSpace(temporaryPasswordPlaintext))
            plain += $" Temporary password: {temporaryPasswordPlaintext}. Please change it after sign-in.";

        await emailService.SendAsync(
            new EmailMessage
            {
                To = toEmail,
                ToName = displayName,
                Subject = "Decorative Plant — staff access",
                BodyPlainText = plain,
                BodyHtml = bodyHtml,
            },
            cancellationToken);
    }

    private static string RoleLabel(string roleCanonical) =>
        string.Join(' ', roleCanonical.Split('_', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => char.ToUpperInvariant(s[0]) + s[1..]));
}
