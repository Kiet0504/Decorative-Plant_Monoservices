namespace decorativeplant_be.Infrastructure.Auth;

public class GoogleOAuthSettings
{
    public const string SectionName = "Google";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Public base URL of this API (no trailing slash), used to compute redirect_uri for Google callback.
    /// Example: https://api.example.com or http://localhost:3000/api
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;
}

