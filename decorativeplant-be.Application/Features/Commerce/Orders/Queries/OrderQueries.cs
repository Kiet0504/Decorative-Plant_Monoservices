using MediatR;
using decorativeplant_be.Application.Common.DTOs.Commerce;
using decorativeplant_be.Application.Common.DTOs.Common;

namespace decorativeplant_be.Application.Features.Commerce.Orders.Queries;

public class GetOrdersQuery : IRequest<PagedResult<OrderResponse>>
{
    public Guid? UserId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid? AssignedStaffId { get; set; }
    /// <summary>
    /// When set with <see cref="BranchId"/>, list orders for that branch that are either unassigned
    /// (fulfillment queue) or assigned to this staff. Omit <see cref="AssignedStaffId"/> when using this.
    /// </summary>
    public Guid? FulfillmentQueueStaffId { get; set; }
    public string? Status { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class GetOrderByIdQuery : IRequest<OrderResponse?>
{
    public Guid Id { get; set; }
    /// <summary>Customer ownership check. Null = staff/admin bypass.</summary>
    public Guid? UserId { get; set; }
    /// <summary>
    /// If set, handler verifies at least one OrderItem belongs to this branch.
    /// Used by branch_manager and fulfillment_staff to prevent cross-branch reads.
    /// </summary>
    public Guid? ActorBranchId { get; set; }
    /// <summary>
    /// If set with <see cref="ActorBranchId"/>, fulfillment may read unassigned orders at that branch or orders assigned to them.
    /// If set without branch, the order must be assigned to this staff member.
    /// </summary>
    public Guid? ActorStaffId { get; set; }
}
