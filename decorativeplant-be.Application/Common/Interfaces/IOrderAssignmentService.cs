using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Application.Common.Interfaces;

/// <summary>
/// Assigns a fulfillment_staff to an order based on workload (fewest active orders).
/// Returns the assigned staff ID, or null when all staff are at capacity.
/// </summary>
public interface IOrderAssignmentService
{
    /// <summary>
    /// Tries to assign the order to the fulfillment_staff with the lowest active workload
    /// at the order's branch. Persists AssignedStaffId on the order and saves.
    /// Returns the assigned UserAccount, or null if no staff has capacity.
    /// </summary>
    Task<UserAccount?> TryAssignAsync(OrderHeader order, CancellationToken ct = default);

    /// <summary>
    /// Called when an order slot frees up (order completed/cancelled/delivered).
    /// Finds the oldest queued order at the same branch and tries to assign it.
    /// No-op if there are no queued orders or all staff are still at capacity.
    /// </summary>
    Task TryFlushQueueAsync(Guid branchId, CancellationToken ct = default);
}
