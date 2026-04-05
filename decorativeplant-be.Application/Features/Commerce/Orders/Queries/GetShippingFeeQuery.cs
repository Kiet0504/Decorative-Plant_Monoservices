using decorativeplant_be.Application.Common.DTOs.Commerce;
using decorativeplant_be.Application.Common.Interfaces;
using MediatR;

namespace decorativeplant_be.Application.Features.Commerce.Orders.Queries;

public record GetShippingFeeQuery(
    int FromDistrictId,
    string FromWardCode,
    int ToDistrictId,
    string ToWardCode,
    int Weight,
    int InsuranceValue
) : IRequest<ShippingFeeResponse>;

public class GetShippingFeeHandler : IRequestHandler<GetShippingFeeQuery, ShippingFeeResponse>
{
    private readonly IShippingService _shippingService;

    public GetShippingFeeHandler(IShippingService shippingService)
    {
        _shippingService = shippingService;
    }

    public async Task<ShippingFeeResponse> Handle(GetShippingFeeQuery request, CancellationToken cancellationToken)
    {
        var feeRequest = new ShippingFeeRequest
        {
            FromDistrictId = request.FromDistrictId > 0 ? request.FromDistrictId : 3695,
            FromWardCode = !string.IsNullOrEmpty(request.FromWardCode) ? request.FromWardCode : "90737",
            ToDistrictId = request.ToDistrictId > 0 ? request.ToDistrictId : 1454,
            ToWardCode = !string.IsNullOrEmpty(request.ToWardCode) ? request.ToWardCode : "21211",
            Weight = request.Weight > 0 ? request.Weight : 1000,
            InsuranceValue = request.InsuranceValue > 0 ? request.InsuranceValue : 500000,
            ServiceTypeId = 2
        };

        return await _shippingService.CalculateFeeAsync(feeRequest);
    }
}
