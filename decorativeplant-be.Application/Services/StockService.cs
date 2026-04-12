using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Application.Services;

public class StockService : IStockService
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<StockService> _logger;

    public StockService(IApplicationDbContext context, ILogger<StockService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task ReserveStockAsync(Guid batchId, int quantity, string productName, CancellationToken ct = default)
    {
        await _context.AcquireStockLockAsync(batchId, ct);
        var stock = await _context.BatchStocks.FirstOrDefaultAsync(s => s.BatchId == batchId, ct);
        if (stock?.Quantities == null) return;

        var (total, reserved, available) = ReadQuantities(stock.Quantities);

        if (available < quantity)
            throw new BadRequestException($"Insufficient stock for '{productName}'. Available: {available}, requested: {quantity}.");

        reserved += quantity;
        available -= quantity;

        WriteQuantities(stock, total, reserved, available);
        _logger.LogInformation("Reserved {Qty} units for BatchId {BatchId}", quantity, batchId);
    }

    public async Task RestoreReservedStockAsync(Guid batchId, int quantity, CancellationToken ct = default)
    {
        await _context.AcquireStockLockAsync(batchId, ct);
        var stock = await _context.BatchStocks.FirstOrDefaultAsync(s => s.BatchId == batchId, ct);
        if (stock?.Quantities == null) return;

        var (total, reserved, available) = ReadQuantities(stock.Quantities);

        reserved -= quantity;
        if (reserved < 0) reserved = 0;
        available += quantity;
        if (available > total) available = total;

        WriteQuantities(stock, total, reserved, available);
        _logger.LogInformation("Restored {Qty} reserved units for BatchId {BatchId}", quantity, batchId);
    }

    public async Task DeductStockAsync(Guid batchId, int quantity, CancellationToken ct = default)
    {
        await _context.AcquireStockLockAsync(batchId, ct);
        var stock = await _context.BatchStocks.FirstOrDefaultAsync(s => s.BatchId == batchId, ct);
        if (stock?.Quantities == null) return;

        var (total, reserved, available) = ReadQuantities(stock.Quantities);

        reserved -= quantity;
        if (reserved < 0) reserved = 0;
        total -= quantity;
        if (total < 0) total = 0;

        WriteQuantities(stock, total, reserved, available);
        _logger.LogInformation("Deducted {Qty} units from BatchId {BatchId} (total: {Total})", quantity, batchId, total);
    }

    public async Task RestoreOrderStockAsync(ICollection<OrderItem> orderItems, CancellationToken ct = default)
    {
        foreach (var item in orderItems)
        {
            if (item.BatchId.HasValue)
                await RestoreReservedStockAsync(item.BatchId.Value, item.Quantity, ct);
        }
    }

    public async Task DeductOrderStockAsync(ICollection<OrderItem> orderItems, CancellationToken ct = default)
    {
        foreach (var item in orderItems)
        {
            if (item.BatchId.HasValue)
                await DeductStockAsync(item.BatchId.Value, item.Quantity, ct);
        }
    }

    private static (int total, int reserved, int available) ReadQuantities(JsonDocument doc)
    {
        var root = doc.RootElement;
        var total = root.TryGetProperty("quantity", out var tq) ? tq.GetInt32() : 0;
        var reserved = root.TryGetProperty("reserved_quantity", out var rq) ? rq.GetInt32() : 0;
        var available = root.TryGetProperty("available_quantity", out var aq) ? aq.GetInt32() : 0;
        return (total, reserved, available);
    }

    private static void WriteQuantities(BatchStock stock, int total, int reserved, int available)
    {
        stock.Quantities = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            quantity = total,
            reserved_quantity = reserved,
            available_quantity = available
        }));
        stock.UpdatedAt = DateTime.UtcNow;
    }
}
