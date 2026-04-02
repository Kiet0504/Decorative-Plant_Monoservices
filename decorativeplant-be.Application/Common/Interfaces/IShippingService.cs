namespace decorativeplant_be.Application.Common.Interfaces;

using decorativeplant_be.Application.Common.DTOs.Commerce;

public interface IShippingService
{
    Task<ShippingFeeResponse> CalculateFeeAsync(ShippingFeeRequest request);
    Task<ShippingOrderResponse> CreateOrderAsync(ShippingOrderRequest request);
}
