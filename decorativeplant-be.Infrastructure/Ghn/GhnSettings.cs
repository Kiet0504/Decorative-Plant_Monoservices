namespace decorativeplant_be.Infrastructure.Ghn;

public class GhnSettings
{
    public const string SectionName = "GhnSettings";
    public string BaseUrl { get; set; } = "https://dev-online-gateway.ghn.vn";
    public string Token { get; set; } = string.Empty;
    public int ShopId { get; set; }
    public string ClientId { get; set; } = string.Empty;
}
