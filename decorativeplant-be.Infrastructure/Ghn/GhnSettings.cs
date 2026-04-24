namespace decorativeplant_be.Infrastructure.Ghn;

public class GhnSettings
{
    public const string SectionName = "GhnSettings";
    public string BaseUrl { get; set; } = "https://dev-online-gateway.ghn.vn";
    public string Token { get; set; } = string.Empty;
    public int ShopId { get; set; }
    public string ClientId { get; set; } = string.Empty;
    // Default origin address for shipments (HCM - Q1 - Ben Nghe)
    public int FromDistrictId { get; set; } = 1454;
    public string FromWardCode { get; set; } = "21211";

    /// <summary>
    /// GHN service type: 2 = E-Commerce (default), 5 = Express.
    /// Override via env GhnSettings__ServiceTypeId.
    /// </summary>
    public int ServiceTypeId { get; set; } = 2;

    /// <summary>
    /// Shared secret GHN sends as the "Token" header on every webhook call.
    /// Configure in the GHN dashboard's "Hook Orders" setting and mirror here
    /// via appsettings / env (GhnSettings__WebhookToken). Empty disables check.
    /// </summary>
    public string WebhookToken { get; set; } = string.Empty;
}
