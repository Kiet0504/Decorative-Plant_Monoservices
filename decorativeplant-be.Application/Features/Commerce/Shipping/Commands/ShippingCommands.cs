using MediatR;
using decorativeplant_be.Application.Common.DTOs.Commerce;

namespace decorativeplant_be.Application.Features.Commerce.Shipping.Commands;

public class CreateShippingCommand : IRequest<ShippingResponse> { public CreateShippingRequest Request { get; set; } = null!; }
public class UpdateShippingStatusCommand : IRequest<ShippingResponse> { public Guid Id { get; set; } public UpdateShippingStatusRequest Request { get; set; } = null!; }

// ── ShippingZone ──
public class CreateShippingZoneCommand : IRequest<ShippingZoneResponse> { public CreateShippingZoneRequest Request { get; set; } = null!; }
public class UpdateShippingZoneCommand : IRequest<ShippingZoneResponse> { public Guid Id { get; set; } public UpdateShippingZoneRequest Request { get; set; } = null!; }
public class DeleteShippingZoneCommand : IRequest<bool> { public Guid Id { get; set; } }
