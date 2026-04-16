namespace decorativeplant_be.Infrastructure.Ghtk;

/// <summary>
/// GHTK (Giao Hang Tiet Kiem) carrier settings. Docs: https://api.ghtk.vn/docs/submit-order/logistic-overview
/// Auth: <c>Token</c> header (shop API token). WebhookToken mirrors the "X-Secure-Token" GHTK
/// posts back so we can authenticate inbound status callbacks.
/// </summary>
public class GhtkSettings
{
    public const string SectionName = "GhtkSettings";

    /// <summary>
    /// Production: <c>https://services.giaohangtietkiem.vn</c>.
    /// Staging/sandbox (partner account): <c>https://services-staging.ghtklab.com</c>.
    /// </summary>
    public string BaseUrl { get; set; } = "https://services.giaohangtietkiem.vn";

    /// <summary>Shop API token issued in GHTK dashboard (sent as <c>Token</c> header).</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>Optional partner code for multi-shop accounts.</summary>
    public string PartnerCode { get; set; } = string.Empty;

    /// <summary>
    /// Default pickup address. GHTK uses plain-text province/district/ward/street
    /// (no ID master data — that's GHN's model).
    /// </summary>
    public string PickupName { get; set; } = "Decorative Plant HQ";
    public string PickupTel { get; set; } = string.Empty;
    public string PickupAddress { get; set; } = string.Empty;
    public string PickupProvince { get; set; } = "TP. Hồ Chí Minh";
    public string PickupDistrict { get; set; } = "Quận 1";
    public string PickupWard { get; set; } = "Phường Bến Nghé";
    public string PickupStreet { get; set; } = string.Empty;

    /// <summary>
    /// Shared secret GHTK sends as <c>X-Secure-Token</c> (or equivalent) on status callbacks.
    /// Configure via GHTK dashboard → Webhook setting. Empty disables verification (dev only).
    /// </summary>
    public string WebhookToken { get; set; } = string.Empty;

    /// <summary>
    /// Transport mode default: <c>road</c> (đường bộ) or <c>fly</c> (đường bay). Affects fee + ETA.
    /// </summary>
    public string Transport { get; set; } = "road";
}
