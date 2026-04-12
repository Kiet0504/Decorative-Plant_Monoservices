using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Application.Services;

/// <summary>
/// Shared service for stock operations with pessimistic locking.
/// Centralizes reserve/restore/deduct logic to prevent duplication and ensure consistency.
/// All methods must be called within an active database transaction.
/// </summary>
public interface IStockService
{
    /// <summary>
    /// Reserves stock for an order item. Acquires pessimistic lock, validates availability,
    /// then increments reserved_quantity and decrements available_quantity.
    /// </summary>
    Task ReserveStockAsync(Guid batchId, int quantity, string productName, CancellationToken ct = default);

    /// <summary>
    /// Restores previously reserved stock (e.g., on cancellation or expiration).
    /// Acquires pessimistic lock, decrements reserved_quantity and increments available_quantity.
    /// </summary>
    Task RestoreReservedStockAsync(Guid batchId, int quantity, CancellationToken ct = default);

    /// <summary>
    /// Finalizes stock deduction after successful payment.
    /// Acquires pessimistic lock, decrements both total quantity and reserved_quantity.
    /// </summary>
    Task DeductStockAsync(Guid batchId, int quantity, CancellationToken ct = default);

    /// <summary>
    /// Restores reserved stock for all items in an order.
    /// </summary>
    Task RestoreOrderStockAsync(ICollection<OrderItem> orderItems, CancellationToken ct = default);

    /// <summary>
    /// Deducts stock for all items in an order after payment confirmation.
    /// </summary>
    Task DeductOrderStockAsync(ICollection<OrderItem> orderItems, CancellationToken ct = default);
}
