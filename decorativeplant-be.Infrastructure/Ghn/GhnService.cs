using System.Text.Json;
using System.Text.Json.Serialization;
using decorativeplant_be.Application.Common.DTOs.Commerce;
using decorativeplant_be.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace decorativeplant_be.Infrastructure.Ghn;

public class GhnService : IShippingService
{
    private readonly HttpClient _httpClient;
    private readonly GhnSettings _settings;
    private readonly ILogger<GhnService> _logger;
    private readonly bool _isConfigured;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public GhnService(HttpClient httpClient, IOptions<GhnSettings> options, ILogger<GhnService> logger)
    {
        _httpClient = httpClient;
        _settings = options.Value;
        _logger = logger;

        if (!string.IsNullOrWhiteSpace(_settings.BaseUrl) && !string.IsNullOrWhiteSpace(_settings.Token))
        {
            _httpClient.BaseAddress = new Uri(_settings.BaseUrl);
            _httpClient.DefaultRequestHeaders.Add("Token", _settings.Token);
            _httpClient.DefaultRequestHeaders.Add("ShopId", _settings.ShopId.ToString());
            _isConfigured = true;
        }
        else
        {
            _logger.LogWarning("GHN is not configured. Shipping features will return defaults.");
            _isConfigured = false;
        }
    }

    public int DefaultFromDistrictId => _settings.FromDistrictId;
    public string DefaultFromWardCode => _settings.FromWardCode;

    public async Task<ShippingFeeResponse> CalculateFeeAsync(ShippingFeeRequest request)
    {
        if (!_isConfigured)
        {
            _logger.LogWarning("GHN not configured, returning default shipping fee of 30000.");
            return new ShippingFeeResponse { Success = true, Total = 30000, ServiceFee = 30000 };
        }

        try
        {
            var body = new
            {
                from_district_id = request.FromDistrictId,
                from_ward_code = request.FromWardCode,
                to_district_id = request.ToDistrictId,
                to_ward_code = request.ToWardCode,
                weight = request.Weight,
                insurance_value = request.InsuranceValue,
                service_type_id = request.ServiceTypeId
            };

            var content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), System.Text.Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("/shiip/public-api/v2/shipping-order/fee", content);
            var responseBody = await response.Content.ReadAsStringAsync();

            _logger.LogDebug("GHN fee response: {Response}", responseBody);

            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;
            var code = root.TryGetProperty("code", out var c) ? c.GetInt32() : -1;

            if (code == 200 && root.TryGetProperty("data", out var data))
            {
                return new ShippingFeeResponse
                {
                    Success = true,
                    Total = data.TryGetProperty("total", out var t) ? t.GetInt32() : 0,
                    ServiceFee = data.TryGetProperty("service_fee", out var sf) ? sf.GetInt32() : 0,
                    InsuranceFee = data.TryGetProperty("insurance_fee", out var inf) ? inf.GetInt32() : 0
                };
            }

            var msg = root.TryGetProperty("message", out var m) ? m.GetString() ?? "Unknown error" : "Unknown error";
            _logger.LogWarning("GHN fee calculation failed: {Message}", msg);
            return new ShippingFeeResponse { Success = false, Message = msg, Total = 30000 };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating GHN shipping fee.");
            return new ShippingFeeResponse { Success = false, Message = ex.Message, Total = 30000 };
        }
    }

    public async Task<ShippingOrderResponse> CreateOrderAsync(ShippingOrderRequest request)
    {
        if (!_isConfigured)
        {
            _logger.LogWarning("GHN not configured, skipping order creation.");
            return new ShippingOrderResponse { Success = false, Message = "GHN not configured" };
        }

        try
        {
            var body = new
            {
                to_name = request.ToName,
                to_phone = request.ToPhone,
                to_address = request.ToAddress,
                to_ward_code = request.ToWardCode,
                to_district_id = request.ToDistrictId,
                from_district_id = request.FromDistrictId,
                from_ward_code = request.FromWardCode,
                weight = request.Weight,
                length = request.Length,
                width = request.Width,
                height = request.Height,
                insurance_value = request.InsuranceValue,
                service_type_id = request.ServiceTypeId,
                payment_type_id = request.PaymentTypeId,
                cod_amount = request.CodAmount,
                cod_failed_amount = request.CodFailedAmount,
                required_note = request.RequiredNote,
                note = request.Note,
                client_order_code = request.ClientOrderCode,
                items = request.Items.Select(i => new { name = i.Name, quantity = i.Quantity, weight = i.Weight }).ToList()
            };

            var content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), System.Text.Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("/shiip/public-api/v2/shipping-order/create", content);
            var responseBody = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("GHN create order response: {Response}", responseBody);

            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;
            var code = root.TryGetProperty("code", out var c) ? c.GetInt32() : -1;

            if (code == 200 && root.TryGetProperty("data", out var data))
            {
                return new ShippingOrderResponse
                {
                    Success = true,
                    OrderCode = data.TryGetProperty("order_code", out var oc) ? oc.GetString() : null,
                    ExpectedDeliveryTime = data.TryGetProperty("expected_delivery_time", out var edt) ? edt.GetString() : null,
                    TotalFee = data.TryGetProperty("total_fee", out var tf) ? tf.GetInt32() : 0
                };
            }

            var msg = root.TryGetProperty("message", out var m) ? m.GetString() ?? "Unknown error" : "Unknown error";
            _logger.LogWarning("GHN create order failed: {Message}", msg);
            return new ShippingOrderResponse { Success = false, Message = msg };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating GHN shipping order.");
            return new ShippingOrderResponse { Success = false, Message = ex.Message };
        }
    }

    // ── GHN Tracking ──

    public async Task<GhnTrackingResponse?> TrackOrderAsync(string ghnOrderCode)
    {
        if (!_isConfigured) return null;
        try
        {
            var content = new StringContent(
                JsonSerializer.Serialize(new { order_code = ghnOrderCode }, JsonOptions),
                System.Text.Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("/shiip/public-api/v2/shipping-order/detail", content);
            var body = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.TryGetProperty("code", out var c) && c.GetInt32() == 200 && root.TryGetProperty("data", out var data))
            {
                var result = new GhnTrackingResponse
                {
                    GhnOrderCode = ghnOrderCode,
                    Status = data.TryGetProperty("status", out var s) ? s.GetString() : null,
                    ExpectedDeliveryTime = data.TryGetProperty("leadtime", out var lt) ? lt.GetString() : null,
                };
                if (data.TryGetProperty("log", out var logs) && logs.ValueKind == JsonValueKind.Array)
                {
                    result.Logs = logs.EnumerateArray().Select(l => new GhnTrackingLog
                    {
                        Status = l.TryGetProperty("status", out var ls) ? ls.GetString() : null,
                        UpdatedDate = l.TryGetProperty("updated_date", out var ud) ? ud.GetString() : null,
                    }).ToList();
                }
                return result;
            }
            var msg = root.TryGetProperty("message", out var m) ? m.GetString() : "Unknown error";
            _logger.LogWarning("GHN tracking failed for {OrderCode}: {Message}", ghnOrderCode, msg);
        }
        catch (Exception ex) { _logger.LogError(ex, "Error tracking GHN order {OrderCode}", ghnOrderCode); }
        return null;
    }

    // ── GHN Switch Status (Sandbox/Dev only) ──

    public async Task<bool> SwitchGhnStatusAsync(string ghnOrderCode, string targetStatus)
    {
        if (!_isConfigured) return false;
        try
        {
            var payload = JsonSerializer.Serialize(new { order_codes = new[] { ghnOrderCode } }, JsonOptions);
            var content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");
            var url = $"/shiip/public-api/v2/switch-status/{targetStatus}";
            _logger.LogInformation("GHN switch-status request: POST {Url} | payload: {Payload}", url, payload);
            var response = await _httpClient.PostAsync(url, content);
            var body = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("GHN switch-status response ({StatusCode}): {Body}", response.StatusCode, body);
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.TryGetProperty("code", out var code) && code.GetInt32() == 200) return true;
            var msg = root.TryGetProperty("message", out var m) ? m.GetString() : "Unknown error";
            _logger.LogWarning("GHN switch-status failed for {OrderCode} → {Status}: {Message}", ghnOrderCode, targetStatus, msg);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error switching GHN status for {OrderCode} → {Status}", ghnOrderCode, targetStatus);
            return false;
        }
    }

    public async Task<string?> PrintOrderAsync(string ghnOrderCode)
    {
        if (!_isConfigured) return null;
        try
        {
            var payload = JsonSerializer.Serialize(new { order_codes = new[] { ghnOrderCode } }, JsonOptions);
            var content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("/shiip/public-api/v2/a5/gen-token", content);
            var body = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.TryGetProperty("code", out var code) && code.GetInt32() == 200 && root.TryGetProperty("data", out var data))
            {
                var token = data.TryGetProperty("token", out var t) ? t.GetString() : null;
                if (!string.IsNullOrEmpty(token))
                {
                    var isDev = _settings.BaseUrl.Contains("dev");
                    var domain = isDev ? "dev-online-gateway.ghn.vn" : "online-gateway.ghn.vn";
                    return $"https://{domain}/a5/public-api/printA5?token={token}";
                }
            }
            _logger.LogWarning("GHN print token generation failed for {OrderCode}", ghnOrderCode);
        }
        catch (Exception ex) { _logger.LogError(ex, "Error generating GHN print token for {OrderCode}", ghnOrderCode); }
        return null;
    }

    public async Task<string?> GetOrderInfoAsync(string ghnOrderCode)
    {
        if (!_isConfigured) return null;
        try
        {
            var payload = JsonSerializer.Serialize(new { order_code = ghnOrderCode }, JsonOptions);
            var content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("/shiip/public-api/v2/shipping-order/detail", content);
            var body = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.TryGetProperty("code", out var code) && code.GetInt32() == 200 && root.TryGetProperty("data", out var data))
            {
                return data.GetRawText();
            }
        }
        catch (Exception ex) { _logger.LogError(ex, "Error fetching GHN order info for {OrderCode}", ghnOrderCode); }
        return null;
    }

    public async Task<bool> UpdateCodAsync(string ghnOrderCode, int newCodAmount)
    {
        if (!_isConfigured) return false;
        try
        {
            var payload = JsonSerializer.Serialize(new { order_code = ghnOrderCode, cod_amount = newCodAmount }, JsonOptions);
            var content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("/shiip/public-api/v2/shipping-order/updateCOD", content);
            var body = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.TryGetProperty("code", out var code) && code.GetInt32() == 200) return true;
            
            var msg = root.TryGetProperty("message", out var m) ? m.GetString() : "Unknown error";
            _logger.LogWarning("GHN update COD failed for {OrderCode} -> {Amount}: {Message}", ghnOrderCode, newCodAmount, msg);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating GHN COD for {OrderCode}", ghnOrderCode);
            return false;
        }
    }

    // ── GHN Advanced APIs (Services, Leadtime, SOC) ──

    public async Task<string?> GetAvailableServicesAsync(int fromDistrictId, int toDistrictId)
    {
        if (!_isConfigured) return null;
        try
        {
            var body = new { shop_id = _settings.ShopId, from_district = fromDistrictId, to_district = toDistrictId };
            var content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), System.Text.Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("/shiip/public-api/v2/shipping-order/available-services", content);
            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching available services from district {From} to {To}", fromDistrictId, toDistrictId);
            return null;
        }
    }

    public async Task<string?> CalculateExpectedDeliveryTimeAsync(int fromDistrictId, string fromWardCode, int toDistrictId, string toWardCode, int serviceId)
    {
        if (!_isConfigured) return null;
        try
        {
            var body = new { from_district_id = fromDistrictId, from_ward_code = fromWardCode, to_district_id = toDistrictId, to_ward_code = toWardCode, service_id = serviceId };
            var content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), System.Text.Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("/shiip/public-api/v2/shipping-order/leadtime", content);
            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching leadtime for service {ServiceId}", serviceId);
            return null;
        }
    }

    public async Task<string?> GetOrderFeeAsync(string ghnOrderCode)
    {
        if (!_isConfigured) return null;
        try
        {
            var body = new { order_code = ghnOrderCode };
            var content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), System.Text.Encoding.UTF8, "application/json");
            // The API requires token and ShopID in the header (which is already configured in _httpClient)
            var response = await _httpClient.PostAsync("/shiip/public-api/v2/shipping-order/soc", content);
            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching SOC fee for order {OrderCode}", ghnOrderCode);
            return null;
        }
    }

    // ── GHN Master Data (Location) ──

    public async Task<List<GhnProvince>> GetProvincesAsync()
    {
        if (!_isConfigured) return new List<GhnProvince>();
        try
        {
            var response = await _httpClient.GetAsync("/shiip/public-api/master-data/province");
            var body = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.TryGetProperty("code", out var c) && c.GetInt32() == 200 && root.TryGetProperty("data", out var data))
            {
                return data.EnumerateArray().Select(p => new GhnProvince(
                    p.TryGetProperty("ProvinceID", out var id) ? id.GetInt32() : 0,
                    p.TryGetProperty("ProvinceName", out var name) ? name.GetString() ?? "" : ""
                )).Where(p => p.ProvinceId > 0).OrderBy(p => p.ProvinceName).ToList();
            }
        }
        catch (Exception ex) { _logger.LogError(ex, "Error fetching GHN provinces"); }
        return new List<GhnProvince>();
    }

    public async Task<List<GhnDistrict>> GetDistrictsAsync(int provinceId)
    {
        if (!_isConfigured) return new List<GhnDistrict>();
        try
        {
            var content = new StringContent(
                JsonSerializer.Serialize(new { province_id = provinceId }, JsonOptions),
                System.Text.Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("/shiip/public-api/master-data/district", content);
            var body = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.TryGetProperty("code", out var c) && c.GetInt32() == 200 && root.TryGetProperty("data", out var data))
            {
                return data.EnumerateArray().Select(d => new GhnDistrict(
                    d.TryGetProperty("DistrictID", out var id) ? id.GetInt32() : 0,
                    d.TryGetProperty("DistrictName", out var name) ? name.GetString() ?? "" : "",
                    provinceId
                )).Where(d => d.DistrictId > 0).OrderBy(d => d.DistrictName).ToList();
            }
        }
        catch (Exception ex) { _logger.LogError(ex, "Error fetching GHN districts for province {ProvinceId}", provinceId); }
        return new List<GhnDistrict>();
    }

    public async Task<List<GhnWard>> GetWardsAsync(int districtId)
    {
        if (!_isConfigured) return new List<GhnWard>();
        try
        {
            var content = new StringContent(
                JsonSerializer.Serialize(new { district_id = districtId }, JsonOptions),
                System.Text.Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("/shiip/public-api/master-data/ward", content);
            var body = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.TryGetProperty("code", out var c) && c.GetInt32() == 200 && root.TryGetProperty("data", out var data))
            {
                return data.EnumerateArray().Select(w => new GhnWard(
                    w.TryGetProperty("WardCode", out var code) ? code.GetString() ?? "" : "",
                    w.TryGetProperty("WardName", out var name) ? name.GetString() ?? "" : "",
                    districtId
                )).Where(w => !string.IsNullOrEmpty(w.WardCode)).OrderBy(w => w.WardName).ToList();
            }
        }
        catch (Exception ex) { _logger.LogError(ex, "Error fetching GHN wards for district {DistrictId}", districtId); }
        return new List<GhnWard>();
    }
}
