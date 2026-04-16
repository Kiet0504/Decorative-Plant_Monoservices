using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using decorativeplant_be.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace decorativeplant_be.Infrastructure.Ghtk;

/// <summary>
/// Real GHTK carrier client. Endpoints per https://api.ghtk.vn/docs/submit-order/logistic-overview:
///   POST /services/shipment/fee            → fee calculation
///   POST /services/shipment/order          → submit order (express)
///   GET  /services/shipment/v2/{tracking}  → status lookup
///   POST /services/shipment/cancel/{id}    → cancel by label_id or partner_id:{clientOrderId}
/// Auth: <c>Token</c> header with shop token.
/// </summary>
public class GhtkService : IGhtkShippingService
{
    private readonly HttpClient _httpClient;
    private readonly GhtkSettings _settings;
    private readonly ILogger<GhtkService> _logger;
    private readonly bool _isConfigured;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public GhtkService(HttpClient httpClient, IOptions<GhtkSettings> options, ILogger<GhtkService> logger)
    {
        _httpClient = httpClient;
        _settings = options.Value;
        _logger = logger;

        if (!string.IsNullOrWhiteSpace(_settings.BaseUrl) && !string.IsNullOrWhiteSpace(_settings.Token))
        {
            _httpClient.BaseAddress = new Uri(_settings.BaseUrl);
            _httpClient.DefaultRequestHeaders.Add("Token", _settings.Token);
            if (!string.IsNullOrWhiteSpace(_settings.PartnerCode))
                _httpClient.DefaultRequestHeaders.Add("X-Client-Source", _settings.PartnerCode);
            _isConfigured = true;
        }
        else
        {
            _logger.LogWarning("GHTK is not configured. GHTK carrier features will return failure.");
            _isConfigured = false;
        }
    }

    public async Task<GhtkFeeResponse> CalculateFeeAsync(GhtkFeeRequest request, CancellationToken ct = default)
    {
        if (!_isConfigured)
            return new GhtkFeeResponse { Success = false, Message = "GHTK not configured", Fee = 30000 };

        try
        {
            // GHTK fee API accepts query string params — not JSON body — per the docs.
            var qs = new Dictionary<string, string?>
            {
                ["pick_province"] = string.IsNullOrWhiteSpace(request.PickProvince) ? _settings.PickupProvince : request.PickProvince,
                ["pick_district"] = string.IsNullOrWhiteSpace(request.PickDistrict) ? _settings.PickupDistrict : request.PickDistrict,
                ["province"]      = request.Province,
                ["district"]      = request.District,
                ["weight"]        = request.Weight.ToString(),
                ["value"]         = request.Value.ToString(),
                ["transport"]     = request.Transport ?? _settings.Transport,
                ["deliver_option"] = request.DeliverOption ?? "none"
            };
            var url = "/services/shipment/fee?" + string.Join("&",
                qs.Where(kv => !string.IsNullOrEmpty(kv.Value))
                  .Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value!)}"));

            var response = await _httpClient.GetAsync(url, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogDebug("GHTK fee response: {Body}", body);

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var ok = root.TryGetProperty("success", out var s) && s.GetBoolean();
            if (!ok)
            {
                var msg = root.TryGetProperty("message", out var m) ? m.GetString() : "GHTK fee failed";
                return new GhtkFeeResponse { Success = false, Message = msg, Fee = 30000 };
            }

            if (!root.TryGetProperty("fee", out var feeObj))
                return new GhtkFeeResponse { Success = true, Fee = 0 };

            return new GhtkFeeResponse
            {
                Success = true,
                Fee = feeObj.TryGetProperty("fee", out var f) && f.ValueKind == JsonValueKind.Number ? f.GetInt32() : 0,
                InsuranceFee = feeObj.TryGetProperty("insurance_fee", out var inf) && inf.ValueKind == JsonValueKind.Number ? inf.GetInt32() : 0,
                DeliverRequired = feeObj.TryGetProperty("delivery", out var d) && d.ValueKind == JsonValueKind.True,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating GHTK fee.");
            return new GhtkFeeResponse { Success = false, Message = ex.Message, Fee = 30000 };
        }
    }

    public async Task<GhtkCreateOrderResponse> CreateOrderAsync(GhtkCreateOrderRequest request, CancellationToken ct = default)
    {
        if (!_isConfigured)
            return new GhtkCreateOrderResponse { Success = false, Message = "GHTK not configured" };

        try
        {
            var body = new
            {
                products = request.Products.Select(p => new
                {
                    name = p.Name,
                    weight = (double)p.Weight / 1000.0, // GHTK expects kg for products
                    quantity = p.Quantity,
                    product_code = p.ProductCode,
                    price = p.Price
                }).ToList(),
                order = new
                {
                    id = request.ClientOrderId,
                    pick_name = request.PickName ?? _settings.PickupName,
                    pick_tel = request.PickTel ?? _settings.PickupTel,
                    pick_address = request.PickAddress ?? _settings.PickupAddress,
                    pick_province = request.PickProvince ?? _settings.PickupProvince,
                    pick_district = request.PickDistrict ?? _settings.PickupDistrict,
                    pick_ward = request.PickWard ?? _settings.PickupWard,
                    pick_street = request.PickStreet ?? _settings.PickupStreet,
                    pick_money = request.PickMoney,
                    name = request.Name,
                    tel = request.Tel,
                    address = request.Address,
                    province = request.Province,
                    district = request.District,
                    ward = request.Ward,
                    street = request.Street,
                    hamlet = request.Hamlet ?? "Khác",
                    note = request.Note,
                    value = request.Value,
                    transport = request.Transport ?? _settings.Transport,
                    deliver_option = request.DeliverOption,
                    pick_option = request.Pick_Option ?? "cod",
                }
            };

            var content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("/services/shipment/order", content, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogInformation("GHTK create-order response ({Status}): {Body}", response.StatusCode, responseBody);

            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;
            var success = root.TryGetProperty("success", out var s) && s.GetBoolean();
            if (!success)
            {
                var msg = root.TryGetProperty("message", out var m) ? m.GetString() : "GHTK submit failed";
                return new GhtkCreateOrderResponse { Success = false, Message = msg };
            }

            if (!root.TryGetProperty("order", out var orderNode))
                return new GhtkCreateOrderResponse { Success = true };

            return new GhtkCreateOrderResponse
            {
                Success = true,
                TrackingId = orderNode.TryGetProperty("tracking_id", out var t) ? t.GetString() : null,
                Label = orderNode.TryGetProperty("label", out var lb) ? lb.GetString() : null,
                Fee = orderNode.TryGetProperty("fee", out var fee) && fee.ValueKind == JsonValueKind.Number ? fee.GetInt32() : 0,
                InsuranceFee = orderNode.TryGetProperty("insurance_fee", out var inf) && inf.ValueKind == JsonValueKind.Number ? inf.GetInt32() : 0,
                EstimatedPickTime = orderNode.TryGetProperty("estimated_pick_time", out var ept) ? ept.GetString() : null,
                EstimatedDeliverTime = orderNode.TryGetProperty("estimated_deliver_time", out var edt) ? edt.GetString() : null,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating GHTK order.");
            return new GhtkCreateOrderResponse { Success = false, Message = ex.Message };
        }
    }

    public async Task<GhtkTrackingResponse?> TrackOrderAsync(string trackingCode, CancellationToken ct = default)
    {
        if (!_isConfigured || string.IsNullOrWhiteSpace(trackingCode)) return null;
        try
        {
            var response = await _httpClient.GetAsync($"/services/shipment/v2/{Uri.EscapeDataString(trackingCode)}", ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogDebug("GHTK track response: {Body}", body);
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var ok = root.TryGetProperty("success", out var s) && s.GetBoolean();
            if (!ok) return null;

            if (!root.TryGetProperty("order", out var o)) return null;

            return new GhtkTrackingResponse
            {
                TrackingId = o.TryGetProperty("tracking_id", out var tid) ? tid.GetString() : trackingCode,
                LabelId = o.TryGetProperty("label_id", out var lid) ? lid.GetString() : null,
                StatusId = o.TryGetProperty("status", out var st) && st.ValueKind == JsonValueKind.Number ? st.GetInt32() : 0,
                StatusText = o.TryGetProperty("status_text", out var stt) ? stt.GetString() : null,
                Message = o.TryGetProperty("message", out var m) ? m.GetString() : null,
                Created = ParseDate(o, "created"),
                Modified = ParseDate(o, "modified"),
                PickDate = ParseDate(o, "pick_date"),
                DeliverDate = ParseDate(o, "deliver_date"),
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error tracking GHTK order {Code}", trackingCode);
            return null;
        }
    }

    public async Task<bool> CancelOrderAsync(string trackingCode, CancellationToken ct = default)
    {
        if (!_isConfigured || string.IsNullOrWhiteSpace(trackingCode)) return false;
        try
        {
            // Empty body; GHTK identifies by path param.
            var response = await _httpClient.PostAsync(
                $"/services/shipment/cancel/{Uri.EscapeDataString(trackingCode)}",
                new StringContent("", Encoding.UTF8, "application/json"), ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogInformation("GHTK cancel response ({Status}): {Body}", response.StatusCode, body);
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            return root.TryGetProperty("success", out var s) && s.GetBoolean();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling GHTK order {Code}", trackingCode);
            return false;
        }
    }

    private static DateTimeOffset? ParseDate(JsonElement obj, string key)
    {
        if (!obj.TryGetProperty(key, out var v) || v.ValueKind != JsonValueKind.String) return null;
        var raw = v.GetString();
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return DateTimeOffset.TryParse(raw, out var dt) ? dt : null;
    }
}
