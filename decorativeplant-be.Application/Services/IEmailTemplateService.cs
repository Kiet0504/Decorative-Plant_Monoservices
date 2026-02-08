namespace decorativeplant_be.Application.Services;

/// <summary>
/// Sends email using named templates (e.g. files) so large or styled HTML lives in template files,
/// not in code. Use for registration OTP, password reset, order confirmation, etc.
/// </summary>
public interface IEmailTemplateService
{
    /// <summary>
    /// Loads the template named <paramref name="templateName"/> (e.g. "RegistrationOtp", "OrderShipped"),
    /// replaces placeholders like {{Code}}, {{UserName}} with values from <paramref name="model"/>,
    /// and sends the email.
    /// </summary>
    /// <param name="templateName">Template name without extension; files should be {templateName}.html and optionally {templateName}.txt.</param>
    /// <param name="model">Key-value pairs for placeholder replacement (e.g. Code, UserName, ExpiresInMinutes).</param>
    /// <param name="to">Recipient email.</param>
    /// <param name="subject">Email subject line.</param>
    /// <param name="toName">Optional recipient display name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SendTemplateAsync(
        string templateName,
        IReadOnlyDictionary<string, string> model,
        string to,
        string subject,
        string? toName = null,
        CancellationToken cancellationToken = default);
}
