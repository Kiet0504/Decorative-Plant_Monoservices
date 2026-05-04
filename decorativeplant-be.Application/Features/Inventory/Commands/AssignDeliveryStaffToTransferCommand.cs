using decorativeplant_be.Application.Features.Inventory.DTOs;
using MediatR;

namespace decorativeplant_be.Application.Features.Inventory.Commands;

/// <summary>
/// Branch manager assigns one fulfillment staff member to confirm dispatch for this transfer.
/// The branch must still have at least two fulfillment staff who are &quot;available&quot;
/// (not already assigned as the deliverer on another outbound awaiting dispatch).
/// </summary>
public class AssignDeliveryStaffToTransferCommand : IRequest<StockTransferDto>
{
    public Guid TransferId { get; set; }

    /// <summary>
    /// The fulfillment staff member who will ship this transfer (<c>delivery_staff_assigned</c> dispatch).
    /// Must belong to the originating branch and be &quot;available&quot; while another colleague stays free for packing.
    /// </summary>
    public Guid StaffId { get; set; }

    /// <summary>Optional display name logged in logistics_info.</summary>
    public string? AssignedByName { get; set; }

    /// <summary>Caller user ID (filled by controller) for verifying branch manager jurisdiction.</summary>
    public Guid? ActingUserId { get; set; }

    /// <summary>JWT role slug (branch_manager vs admin).</summary>
    public string? ActingRole { get; set; }
}
