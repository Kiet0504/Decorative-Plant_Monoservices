namespace decorativeplant_be.Infrastructure.Auth;

public class FrontendSettings
{
    public const string SectionName = "Frontend";

    /// <summary>
    /// Public base URL of the frontend (no trailing slash). Example: http://localhost:5173
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;
}

