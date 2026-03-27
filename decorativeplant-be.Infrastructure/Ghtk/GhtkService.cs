using System.Net.Http.Headers;
using System.Text.Json;
using decorativeplant_be.Application.Common.DTOs.Commerce;
using decorativeplant_be.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace decorativeplant_be.Infrastructure.Ghtk;

public class GhtkService : IGhtkService
{
    private readonly HttpClient _httpClient;
    private readonly GhtkSettings _settings;
    private readonly ILogger<GhtkService> _logger;

    public GhtkService(HttpClient httpClient, IOptions<GhtkSettings> options, ILogger<GhtkService> logger)
    {
        _httpClient = httpClient;
        _settings = options.Value;
        _logger = logger;

        _httpClient.BaseAddress = new Uri(_settings.BaseUrl);
        _httpClient.DefaultRequestHeaders.Add("Token", _settings.Token);
    }

    public async Task<GhtkFeeResponse> CalculateFeeAsync(GhtkFeeRequest request)
    {
        try
        {
            var url = $"/services/shipment/fee?pick_province={Uri.EscapeDataString(request.PickProvince)}&pick_district={Uri.EscapeDataString(request.PickDistrict)}&province={Uri.EscapeDataString(request.Province)}&district={Uri.EscapeDataString(request.District)}&address={Uri.EscapeDataString(request.Address)}&weight={request.Weight}&value={request.Value}&deliver_option=none";
            
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var result = JsonSerializer.Deserialize<GhtkFeeResponse>(content, options);

            return result ?? new GhtkFeeResponse { Success = false, Message = "Failed to deserialize response." };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating GHTK fee.");
            return new GhtkFeeResponse { Success = false, Message = ex.Message };
        }
    }

    public async Task<GhtkOrderResponse> CreateOrderAsync(GhtkOrderRequest request)
    {
        try
        {
            var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var json = JsonSerializer.Serialize(request, options);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/services/shipment/order", content);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<GhtkOrderResponse>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return result ?? new GhtkOrderResponse { Success = false, Message = "Failed to deserialize response." };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating GHTK order.");
            return new GhtkOrderResponse { Success = false, Message = ex.Message };
        }
    }
}
