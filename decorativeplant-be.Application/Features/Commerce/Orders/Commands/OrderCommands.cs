using MediatR;
using decorativeplant_be.Application.Common.DTOs.Commerce;

namespace decorativeplant_be.Application.Features.Commerce.Orders.Commands;

public class CreateOrderCommand : IRequest<OrderResponse>
{
    public Guid UserId { get; set; }
    public CreateOrderRequest Request { get; set; } = null!;
}

public class UpdateOrderStatusCommand : IRequest<OrderResponse>
{
    public Guid Id { get; set; }
    public UpdateOrderStatusRequest Request { get; set; } = null!;
}

public class CancelOrderCommand : IRequest<OrderResponse>
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public CancelOrderRequest Request { get; set; } = null!;
}
