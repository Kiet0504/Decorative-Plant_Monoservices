namespace decorativeplant_be.Infrastructure.Auth;

public class GoogleOAuthSettings
{
    public const string SectionName = "Google";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Public base URL of this API (no trailing slash), including /api.
    /// Example: http://localhost:8080/api or https://api.example.com/api
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;
}

