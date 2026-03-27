namespace decorativeplant_be.Application.Common.Interfaces;

using decorativeplant_be.Application.Common.DTOs.Commerce;
using System.Threading.Tasks;

public interface IGhtkService
{
    Task<GhtkFeeResponse> CalculateFeeAsync(GhtkFeeRequest request);
    Task<GhtkOrderResponse> CreateOrderAsync(GhtkOrderRequest request);
}
