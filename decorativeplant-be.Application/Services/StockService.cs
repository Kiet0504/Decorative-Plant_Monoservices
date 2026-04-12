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

    public async Task ReserveStockAsync(Guid listingId, Guid? branchId, int quantity, string productName, CancellationToken ct = default)
    {
        // 1. Pessimistic lock on the ProductListing row
        await _context.AcquireStockLockAsync(listingId, ct);

        // 2. Load ProductListing and deduct system-wide stock_quantity
        var listing = await _context.ProductListings
            .FirstOrDefaultAsync(l => l.Id == listingId, ct);
        if (listing?.ProductInfo == null)
            throw new NotFoundException($"Listing {listingId} not found.");

        var listingStock = GetListingStockQuantity(listing.ProductInfo);
        if (listingStock < quantity)
            throw new BadRequestException($"Insufficient stock for '{productName}'. Available (system): {listingStock}, requested: {quantity}.");

        SetListingStockQuantity(listing, listingStock - quantity);

        // 3. Deduct branch-level available_quantity in BatchStock
        //    reserved_quantity is NOT touched — it represents plants not for sale (staff-managed)
        if (listing.BatchId.HasValue && branchId.HasValue)
        {
            var batchStock = await FindBatchStockAsync(listing.BatchId.Value, branchId.Value, ct);
            if (batchStock?.Quantities != null)
            {
                var quantities = ReadQuantities(batchStock.Quantities);

                if (quantities.Available < quantity)
                    throw new BadRequestException($"Insufficient stock for '{productName}' at branch. Available: {quantities.Available}, requested: {quantity}.");

                quantities.Available -= quantity;
                WriteQuantities(batchStock, quantities);
            }
        }

        _logger.LogInformation("Reserved {Qty} units for Listing {ListingId} at Branch {BranchId}", quantity, listingId, branchId);
    }

    public async Task RestoreReservedStockAsync(Guid listingId, Guid? branchId, int quantity, CancellationToken ct = default)
    {
        await _context.AcquireStockLockAsync(listingId, ct);

        // 1. Restore system-wide stock_quantity
        var listing = await _context.ProductListings
            .FirstOrDefaultAsync(l => l.Id == listingId, ct);
        if (listing?.ProductInfo != null)
        {
            var listingStock = GetListingStockQuantity(listing.ProductInfo);
            SetListingStockQuantity(listing, listingStock + quantity);
        }

        // 2. Restore branch-level available_quantity
        if (listing?.BatchId != null && branchId.HasValue)
        {
            var batchStock = await FindBatchStockAsync(listing.BatchId.Value, branchId.Value, ct);
            if (batchStock?.Quantities != null)
            {
                var quantities = ReadQuantities(batchStock.Quantities);
                quantities.Available += quantity;
                WriteQuantities(batchStock, quantities);
            }
        }

        _logger.LogInformation("Restored {Qty} units for Listing {ListingId} at Branch {BranchId}", quantity, listingId, branchId);
    }

    public async Task DeductStockAsync(Guid listingId, Guid? branchId, int quantity, CancellationToken ct = default)
    {
        // Finalizes after delivery/completion: plants physically left the branch.
        // ProductListing.stock_quantity was already decremented during reservation.
        // Here we only reduce BatchStock.quantity (total at branch).
        await _context.AcquireStockLockAsync(listingId, ct);

        var listing = await _context.ProductListings
            .FirstOrDefaultAsync(l => l.Id == listingId, ct);

        if (listing?.BatchId != null && branchId.HasValue)
        {
            var batchStock = await FindBatchStockAsync(listing.BatchId.Value, branchId.Value, ct);
            if (batchStock?.Quantities != null)
            {
                var quantities = ReadQuantities(batchStock.Quantities);
                quantities.Total -= quantity;
                if (quantities.Total < 0) quantities.Total = 0;
                WriteQuantities(batchStock, quantities);
            }
        }

        _logger.LogInformation("Deducted {Qty} units for Listing {ListingId} at Branch {BranchId}", quantity, listingId, branchId);
    }

    public async Task RestoreOrderStockAsync(ICollection<OrderItem> orderItems, CancellationToken ct = default)
    {
        foreach (var item in orderItems)
        {
            if (item.ListingId.HasValue)
                await RestoreReservedStockAsync(item.ListingId.Value, item.BranchId, item.Quantity, ct);
        }
    }

    public async Task DeductOrderStockAsync(ICollection<OrderItem> orderItems, CancellationToken ct = default)
    {
        foreach (var item in orderItems)
        {
            if (item.ListingId.HasValue)
                await DeductStockAsync(item.ListingId.Value, item.BranchId, item.Quantity, ct);
        }
    }

    // ── Helpers: BatchStock ──

    private async Task<BatchStock?> FindBatchStockAsync(Guid batchId, Guid branchId, CancellationToken ct)
    {
        return await _context.BatchStocks
            .Include(bs => bs.Location)
            .Where(bs => bs.BatchId == batchId && bs.Location != null && bs.Location.BranchId == branchId)
            .FirstOrDefaultAsync(ct);
    }

    // Struct to hold parsed BatchStock quantities
    private class StockQuantities
    {
        public int Total { get; set; }
        public int Reserved { get; set; }
        public int Available { get; set; }
        public int? TotalReceived { get; set; }
    }

    private static StockQuantities ReadQuantities(JsonDocument doc)
    {
        var root = doc.RootElement;
        return new StockQuantities
        {
            Total = root.TryGetProperty("quantity", out var tq) ? tq.GetInt32() : 0,
            Reserved = root.TryGetProperty("reserved_quantity", out var rq) ? rq.GetInt32() : 0,
            Available = root.TryGetProperty("available_quantity", out var aq) ? aq.GetInt32() : 0,
            TotalReceived = root.TryGetProperty("total_received", out var tr) ? tr.GetInt32() : null
        };
    }

    private static void WriteQuantities(BatchStock stock, StockQuantities quantities)
    {
        var dict = new Dictionary<string, int>
        {
            ["quantity"] = quantities.Total,
            ["reserved_quantity"] = quantities.Reserved,  // preserved as-is (staff-managed)
            ["available_quantity"] = quantities.Available
        };
        if (quantities.TotalReceived.HasValue)
            dict["total_received"] = quantities.TotalReceived.Value;

        stock.Quantities = JsonDocument.Parse(JsonSerializer.Serialize(dict));
        stock.UpdatedAt = DateTime.UtcNow;
    }

    // ── Helpers: ProductListing ──

    private static int GetListingStockQuantity(JsonDocument productInfo)
    {
        return productInfo.RootElement.TryGetProperty("stock_quantity", out var sq) ? sq.GetInt32() : 0;
    }

    private static void SetListingStockQuantity(ProductListing listing, int newStockQuantity)
    {
        if (listing.ProductInfo == null) return;

        var info = JsonSerializer.Deserialize<Dictionary<string, object>>(
            listing.ProductInfo.RootElement.GetRawText()) ?? new();
        info["stock_quantity"] = newStockQuantity;
        listing.ProductInfo = JsonDocument.Parse(JsonSerializer.Serialize(info));
    }
}
