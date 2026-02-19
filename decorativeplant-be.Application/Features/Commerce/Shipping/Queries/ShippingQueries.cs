using MediatR;
using decorativeplant_be.Application.Common.DTOs.Commerce;

namespace decorativeplant_be.Application.Features.Commerce.Shipping.Queries;

public class GetShippingByOrderQuery : IRequest<List<ShippingResponse>> { public Guid OrderId { get; set; } }
public class GetShippingZonesQuery : IRequest<List<ShippingZoneResponse>> { public Guid? BranchId { get; set; } }
public class GetShippingZoneByIdQuery : IRequest<ShippingZoneResponse?> { public Guid Id { get; set; } }
