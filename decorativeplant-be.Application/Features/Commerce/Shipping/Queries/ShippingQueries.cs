using MediatR;
using decorativeplant_be.Application.Common.DTOs.Commerce;

using decorativeplant_be.Application.Common.DTOs.Common;

namespace decorativeplant_be.Application.Features.Commerce.Shipping.Queries;

public class GetShippingByOrderQuery : IRequest<List<ShippingResponse>> { public Guid OrderId { get; set; } }
public class GetShippingZonesQuery : IRequest<PagedResult<ShippingZoneResponse>> 
{ 
    public Guid? BranchId { get; set; } 
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
public class GetShippingZoneByIdQuery : IRequest<ShippingZoneResponse?> { public Guid Id { get; set; } }
