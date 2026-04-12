using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Application.Services;

/// <summary>
/// Shared service for stock operations with pessimistic locking.
/// Centralizes reserve/restore/deduct logic to prevent duplication and ensure consistency.
/// All methods must be called within an active database transaction.
/// 
/// Stock is managed at two levels:
/// - ProductListing.ProductInfo.stock_quantity: system-wide total available
/// - BatchStock.Quantities.available_quantity: per-branch available (at the owning branch)
/// </summary>
public interface IStockService
{
    /// <summary>
    /// Reserves stock for an order item. Acquires pessimistic lock on ProductListing,
    /// then decrements stock_quantity (system-wide) and available_quantity (branch-level),
    /// while incrementing reserved_quantity in BatchStock.
    /// </summary>
    Task ReserveStockAsync(Guid listingId, Guid? branchId, int quantity, string productName, CancellationToken ct = default);

    /// <summary>
    /// Restores previously reserved stock (e.g., on cancellation or expiration).
    /// Increments stock_quantity (system-wide) and available_quantity (branch-level),
    /// while decrementing reserved_quantity in BatchStock.
    /// </summary>
    Task RestoreReservedStockAsync(Guid listingId, Guid? branchId, int quantity, CancellationToken ct = default);

    /// <summary>
    /// Finalizes stock deduction after successful delivery/completion.
    /// Decrements total quantity and reserved_quantity in BatchStock.
    /// ProductListing.stock_quantity was already decremented during reservation.
    /// </summary>
    Task DeductStockAsync(Guid listingId, Guid? branchId, int quantity, CancellationToken ct = default);

    /// <summary>
    /// Restores reserved stock for all items in an order.
    /// </summary>
    Task RestoreOrderStockAsync(ICollection<OrderItem> orderItems, CancellationToken ct = default);

    /// <summary>
    /// Deducts stock for all items in an order after payment confirmation.
    /// </summary>
    Task DeductOrderStockAsync(ICollection<OrderItem> orderItems, CancellationToken ct = default);
}
