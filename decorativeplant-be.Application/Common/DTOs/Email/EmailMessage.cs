namespace decorativeplant_be.Application.Common.DTOs.Email;

/// <summary>
/// Represents an email to be sent. Use for all outbound email to keep a single contract;
/// swap implementations (SMTP, SendGrid, SES) without changing callers.
/// </summary>
public class EmailMessage
{
    public string To { get; set; } = string.Empty;
    public string? ToName { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string BodyPlainText { get; set; } = string.Empty;
    public string? BodyHtml { get; set; }
    /// <summary>Optional template identifier for providers that support templates (e.g. SendGrid).</summary>
    public string? TemplateId { get; set; }
    /// <summary>Template data when using template-based sending.</summary>
    public IReadOnlyDictionary<string, object>? TemplateData { get; set; }
}
