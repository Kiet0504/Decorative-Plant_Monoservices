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
                weight = request.Weight,
                length = request.Length,
                width = request.Width,
                height = request.Height,
                insurance_value = request.InsuranceValue,
                service_type_id = request.ServiceTypeId,
                payment_type_id = request.PaymentTypeId,
                required_note = request.RequiredNote,
                note = request.Note,
                client_order_code = request.ClientOrderCode,
                items = request.Items.Select(i => new { name = i.Name, quantity = i.Quantity, weight = i.Weight }).ToList()
            };

            var content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), System.Text.Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("/shiip/public-api/v2/shipping-order/create", content);
            var responseBody = await response.Content.ReadAsStringAsync();

            _logger.LogDebug("GHN create order response: {Response}", responseBody);

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
}
