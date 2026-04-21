using MediatR;
using decorativeplant_be.Application.Common.DTOs.Commerce;

namespace decorativeplant_be.Application.Features.Commerce.Orders.Commands;

public class CreateOrderCommand : IRequest<List<OrderResponse>>
{
    public Guid? UserId { get; set; }
    public CreateOrderRequest Request { get; set; } = null!;
}

public class UpdateOrderStatusCommand : IRequest<OrderResponse>
{
    public Guid Id { get; set; }
    public Guid? ActorUserId { get; set; }
    public UpdateOrderStatusRequest Request { get; set; } = null!;
}

public class CancelOrderCommand : IRequest<OrderResponse>
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public CancelOrderRequest Request { get; set; } = null!;
}

public class ConfirmReceiptCommand : IRequest<OrderResponse>
{
    public Guid OrderId { get; set; }
    public Guid UserId { get; set; }
}

public class ConfirmReceiptBatchCommand : IRequest<List<OrderResponse>>
{
    public List<Guid> OrderIds { get; set; } = new();
    public Guid UserId { get; set; }
}

public class CreateOfflineBopisOrderCommand : IRequest<OrderResponse>
{
    public Guid BrandManagerId { get; set; }
    public CreateOfflineBopisRequest Request { get; set; } = null!;
}

public class MarkOrderPickedUpCommand : IRequest<OrderResponse>
{
    public Guid OrderId { get; set; }
    public Guid StaffUserId { get; set; }
    public MarkOrderPickedUpRequest Request { get; set; } = null!;
}

public class ManualAssignOrderCommand : IRequest<OrderResponse>
{
    public Guid OrderId { get; set; }
    /// <summary>Branch manager making the assignment.</summary>
    public Guid ManagerId { get; set; }
    /// <summary>fulfillment_staff to assign.</summary>
    public Guid StaffId { get; set; }
}

public class CreateBopisImmediateOrderCommand : IRequest<OrderResponse>
{
    public Guid StaffUserId { get; set; }
    public CreateBopisImmediateRequest Request { get; set; } = null!;
}

public class CompleteOrderCommand : IRequest<OrderResponse>
{
    public Guid OrderId { get; set; }
    public Guid StaffUserId { get; set; }
    public CompleteOrderRequest Request { get; set; } = null!;
}
