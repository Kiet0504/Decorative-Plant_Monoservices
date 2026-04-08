namespace decorativeplant_be.Application.Common.Interfaces;

using decorativeplant_be.Application.Common.DTOs.Commerce;

public interface IShippingService
{
    int DefaultFromDistrictId { get; }
    string DefaultFromWardCode { get; }
    Task<ShippingFeeResponse> CalculateFeeAsync(ShippingFeeRequest request);
    Task<ShippingOrderResponse> CreateOrderAsync(ShippingOrderRequest request);
    Task<List<GhnProvince>> GetProvincesAsync();
    Task<List<GhnDistrict>> GetDistrictsAsync(int provinceId);
    Task<List<GhnWard>> GetWardsAsync(int districtId);
    Task<GhnTrackingResponse?> TrackOrderAsync(string ghnOrderCode);
    Task<bool> SwitchGhnStatusAsync(string ghnOrderCode, string targetStatus);
}
