namespace decorativeplant_be.Application.Common.Options;

/// <summary>
/// Public SPA base URL for links in customer emails (same config section as infrastructure FrontendSettings).
/// </summary>
public class CustomerPortalLinksOptions
{
    public const string SectionName = "Frontend";

    /// <summary>Example: https://app.example.com — no trailing slash.</summary>
    public string BaseUrl { get; set; } = string.Empty;
}
