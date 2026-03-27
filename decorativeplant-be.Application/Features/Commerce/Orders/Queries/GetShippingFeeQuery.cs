using decorativeplant_be.Application.Common.DTOs.Commerce;
using decorativeplant_be.Application.Common.Interfaces;
using MediatR;

namespace decorativeplant_be.Application.Features.Commerce.Orders.Queries;

public record GetShippingFeeQuery(
    string PickProvince,
    string PickDistrict,
    string Province,
    string District,
    string Address,
    int Weight,
    int Value
) : IRequest<GhtkFeeResponse>;

public class GetShippingFeeHandler : IRequestHandler<GetShippingFeeQuery, GhtkFeeResponse>
{
    private readonly IGhtkService _ghtkService;

    public GetShippingFeeHandler(IGhtkService ghtkService)
    {
        _ghtkService = ghtkService;
    }

    public async Task<GhtkFeeResponse> Handle(GetShippingFeeQuery request, CancellationToken cancellationToken)
    {
        var feeRequest = new GhtkFeeRequest
        {
            PickProvince = request.PickProvince,
            PickDistrict = request.PickDistrict,
            Province = request.Province,
            District = request.District,
            Address = string.IsNullOrEmpty(request.Address) ? "Unknown" : request.Address,
            Weight = request.Weight > 0 ? request.Weight : 1000, // default 1kg
            Value = request.Value > 0 ? request.Value : 500000 // default value
        };

        return await _ghtkService.CalculateFeeAsync(feeRequest);
    }
}
