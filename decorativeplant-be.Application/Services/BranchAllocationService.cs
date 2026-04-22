using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Application.Services;

/// <summary>
/// Represents an allocation result: which listing (and therefore which branch) 
/// should fulfill how many units of a product.
/// </summary>
public class AllocationResult
{
    public ProductListing Listing { get; set; } = null!;
    public int AllocatedQuantity { get; set; }
    public string UnitPrice { get; set; } = "0";
    public string? Title { get; set; }
    public string? Image { get; set; }
}

/// <summary>
/// Chain Store model: automatically allocates items to branches based on stock availability.
/// Strategy: "Minimize Splits" — try to fulfill the entire cart from as few branches as possible.
/// Tiebreaker: branch with more total stock wins.
/// Fallback: if no single branch has all items, use greedy set-cover.
/// </summary>
public interface IBranchAllocationService
{
    /// <summary>
    /// Given a list of (listingId, quantity) from the customer's cart, resolve each to 
    /// the optimal branch listing(s) and return the allocation.
    /// </summary>
    Task<List<AllocationResult>> AllocateAsync(
        List<(Guid ListingId, int Quantity)> requestedItems,
        CancellationToken ct,
        Guid? fulfillFromBranchId = null);
}

public class BranchAllocationService : IBranchAllocationService
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<BranchAllocationService> _logger;

    public BranchAllocationService(IApplicationDbContext context, ILogger<BranchAllocationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    // ── Helper: extract product info from a listing ──
    private static (string? title, string unitPrice, string? image) ExtractProductInfo(ProductListing listing)
    {
        string? title = null;
        string unitPrice = "0";
        string? image = null;

        if (listing.ProductInfo != null)
        {
            var root = listing.ProductInfo.RootElement;
            title = root.TryGetProperty("title", out var t) ? t.GetString() : null;
            unitPrice = root.TryGetProperty("price", out var p) ? p.GetString() ?? "0" : "0";
        }
        if (listing.Images?.RootElement.ValueKind == JsonValueKind.Array)
        {
            var first = listing.Images.RootElement.EnumerateArray().FirstOrDefault();
            image = first.TryGetProperty("url", out var u) ? u.GetString() : null;
        }

        return (title, unitPrice, image);
    }

    // ── Helper: get available stock — prioritize ProductListing.stock_quantity (source of truth for orders) ──
    private static int GetAvailableStockFromMap(ProductListing listing, Dictionary<Guid, BatchStock> stockMap)
    {
        // Primary: use stock_quantity from ProductInfo (system-wide source of truth)
        if (listing.ProductInfo != null)
        {
            if (listing.ProductInfo.RootElement.TryGetProperty("stock_quantity", out var sq))
                return sq.GetInt32();
        }

        // Fallback: check BatchStock available_quantity
        if (listing.BatchId.HasValue && stockMap.TryGetValue(listing.BatchId.Value, out var stock))
        {
            if (stock.Quantities != null)
            {
                return stock.Quantities.RootElement.TryGetProperty("available_quantity", out var aq)
                    ? aq.GetInt32() : 0;
            }
        }
        return 0;
    }

    private static bool IsActiveListing(ProductListing listing)
    {
        if (listing.StatusInfo == null) return false;
        return listing.StatusInfo.RootElement.TryGetProperty("status", out var st) && st.GetString() == "active";
    }

    public async Task<List<AllocationResult>> AllocateAsync(
        List<(Guid ListingId, int Quantity)> requestedItems,
        CancellationToken ct,
        Guid? fulfillFromBranchId = null)
    {
        // ══════════════════════════════════════════════════════════
        // Phase 1: Resolve each cart item to its product title
        //          and find all sibling listings across branches
        // ══════════════════════════════════════════════════════════

        // Load only the requested listings first (not all listings)
        var requestedIds = requestedItems.Select(r => r.ListingId).ToList();
        var primaryListings = await _context.ProductListings
            .Include(l => l.Batch)
            .Include(l => l.Branch)
            .Where(l => requestedIds.Contains(l.Id))
            .ToListAsync(ct);

        // Collect TaxonomyIds for sibling matching (preferred over title matching)
        var taxonomyIds = primaryListings
            .Where(l => l.Batch?.TaxonomyId != null)
            .Select(l => l.Batch!.TaxonomyId!.Value)
            .Distinct()
            .ToList();

        // Also collect titles as fallback for listings without taxonomy
        var titles = primaryListings
            .Select(l => ExtractProductInfo(l).title)
            .Where(t => !string.IsNullOrEmpty(t))
            .Select(t => t!.Trim().ToLowerInvariant())
            .Distinct()
            .ToList();

        var siblingListings = await _context.ProductListings
            .Include(l => l.Batch)
            .Include(l => l.Branch)
            .Where(l => !requestedIds.Contains(l.Id) && (
                (l.Batch != null && l.Batch.TaxonomyId != null && taxonomyIds.Contains(l.Batch.TaxonomyId.Value))
            ))
            .ToListAsync(ct);

        // Filter out any listings that are NOT active (Draft status)
        var allRelevantListings = primaryListings
            .Concat(siblingListings)
            .Where(IsActiveListing)
            .ToList();

        // Batch-load all stock data in one query instead of N+1
        var allBatchIds = allRelevantListings
            .Where(l => l.BatchId.HasValue)
            .Select(l => l.BatchId!.Value)
            .Distinct()
            .ToList();
        var stockMap = await _context.BatchStocks
            .Where(s => s.BatchId.HasValue && allBatchIds.Contains(s.BatchId.Value))
            .ToDictionaryAsync(s => s.BatchId!.Value, ct);

        // For each requested item, collect: title, price, image, quantity needed
        var cartProducts = new List<CartProduct>();

        foreach (var (listingId, qty) in requestedItems)
        {
            var primaryListing = primaryListings.FirstOrDefault(l => l.Id == listingId)
                ?? throw new NotFoundException($"Listing {listingId} not found.");

            var (title, price, image) = ExtractProductInfo(primaryListing);

            if (string.IsNullOrEmpty(title))
            {
                if (fulfillFromBranchId.HasValue && primaryListing.BranchId != fulfillFromBranchId.Value)
                {
                    throw new BadRequestException(
                        $"Listing {listingId} is not stocked at the selected branch.");
                }

                var primaryStock = GetAvailableStockFromMap(primaryListing, stockMap);
                if (fulfillFromBranchId.HasValue && primaryStock < qty)
                {
                    throw new BadRequestException(
                        $"Insufficient stock at branch for listing {listingId}. Available: {primaryStock}, requested: {qty}.");
                }

                var stockForPhase2 = fulfillFromBranchId.HasValue ? primaryStock : qty;
                cartProducts.Add(new CartProduct
                {
                    Title = title ?? listingId.ToString(),
                    UnitPrice = price,
                    Image = image,
                    QuantityNeeded = qty,
                    SiblingListings = new List<(ProductListing listing, int stock)>
                    {
                        (primaryListing, stockForPhase2)
                    }
                });
                continue;
            }

            // Find siblings by TaxonomyId (preferred) or title fallback
            var siblings = allRelevantListings
                .Where(l =>
                {
                    // Prefer TaxonomyId matching (reliable, avoids name collision)
                    if (primaryListing.Batch?.TaxonomyId != null && l.Batch?.TaxonomyId != null)
                        return l.Batch.TaxonomyId == primaryListing.Batch.TaxonomyId;

                    // Fallback: title matching
                    if (l.ProductInfo == null) return false;
                    var t = l.ProductInfo.RootElement.TryGetProperty("title", out var tv)
                        ? tv.GetString() : null;
                    return string.Equals(t?.Trim(), title.Trim(), StringComparison.OrdinalIgnoreCase);
                })
                .ToList();

            // Get stock for each sibling from pre-loaded map
            var siblingsWithStock = new List<(ProductListing listing, int stock)>();
            foreach (var s in siblings)
            {
                var stock = GetAvailableStockFromMap(s, stockMap);
                if (stock > 0)
                    siblingsWithStock.Add((s, stock));
            }

            if (fulfillFromBranchId.HasValue)
            {
                siblingsWithStock = siblingsWithStock
                    .Where(s => s.listing.BranchId == fulfillFromBranchId.Value)
                    .ToList();
            }

            // Validate total stock
            int totalAvailable = siblingsWithStock.Sum(x => x.stock);
            if (totalAvailable < qty)
            {
                throw new BadRequestException(
                    $"Insufficient stock for '{title}'. Available: {totalAvailable}, requested: {qty}.");
            }

            cartProducts.Add(new CartProduct
            {
                Title = title,
                UnitPrice = price,
                Image = image,
                QuantityNeeded = qty,
                SiblingListings = siblingsWithStock
            });
        }

        // ══════════════════════════════════════════════════════════
        // Phase 2: Minimize Splits — Greedy Set Cover
        //   Goal: assign all items using as few branches as possible
        // ══════════════════════════════════════════════════════════

        // Collect all unique branch IDs that appear in any sibling listing
        var allBranchIds = cartProducts
            .SelectMany(cp => cp.SiblingListings.Select(s => s.listing.BranchId))
            .Where(b => b.HasValue)
            .Select(b => b!.Value)
            .Distinct()
            .ToList();

        // For each branch, check which cart products it can fulfill (has enough stock)
        var branchCapabilities = new Dictionary<Guid, BranchCapability>();
        foreach (var branchId in allBranchIds)
        {
            var capability = new BranchCapability { BranchId = branchId };

            foreach (var cp in cartProducts)
            {
                var branchListing = cp.SiblingListings
                    .FirstOrDefault(s => s.listing.BranchId == branchId);

                if (branchListing.listing != null && branchListing.stock >= cp.QuantityNeeded)
                {
                    capability.CanFulfill.Add(cp.Title);
                    capability.TotalStock += branchListing.stock;
                }
            }

            branchCapabilities[branchId] = capability;
        }

        // Greedy: repeatedly pick the branch that covers the most unfulfilled items
        var results = new List<AllocationResult>();
        var unfulfilled = new HashSet<string>(cartProducts.Select(cp => cp.Title));

        while (unfulfilled.Count > 0)
        {
            // Score each branch: how many unfulfilled items can it cover?
            var bestBranch = branchCapabilities
                .Where(kvp => kvp.Value.CanFulfill.Any(t => unfulfilled.Contains(t)))
                .OrderByDescending(kvp => kvp.Value.CanFulfill.Count(t => unfulfilled.Contains(t))) // most coverage
                .ThenByDescending(kvp => kvp.Value.TotalStock) // tiebreaker: most total stock
                .Select(kvp => (Guid?)kvp.Key)
                .FirstOrDefault();

            if (bestBranch == null)
            {
                // No branch can cover any remaining item as a whole.
                // Fallback: allocate remaining items using "Largest Stock First" per item.
                foreach (var title in unfulfilled.ToList())
                {
                    var cp = cartProducts.First(c => c.Title == title);
                    AllocateLargestStockFirst(cp, results);
                    unfulfilled.Remove(title);
                }
                break;
            }

            // Assign all items this branch can fulfill
            var branchId = bestBranch.Value;
            var coveredTitles = branchCapabilities[branchId].CanFulfill
                .Where(t => unfulfilled.Contains(t))
                .ToList();

            foreach (var title in coveredTitles)
            {
                var cp = cartProducts.First(c => c.Title == title);
                var branchListing = cp.SiblingListings
                    .First(s => s.listing.BranchId == branchId);

                results.Add(new AllocationResult
                {
                    Listing = branchListing.listing,
                    AllocatedQuantity = cp.QuantityNeeded,
                    UnitPrice = cp.UnitPrice,
                    Title = cp.Title,
                    Image = cp.Image
                });

                unfulfilled.Remove(title);

                _logger.LogInformation(
                    "MinimizeSplits: Allocated {Qty} of '{Title}' from Branch {BranchId} (stock: {Stock})",
                    cp.QuantityNeeded, title, branchId, branchListing.stock);
            }
        }

        return results;
    }

    /// <summary>
    /// Fallback: when no single branch can cover an item's full quantity, 
    /// split across branches starting from the one with the most stock.
    /// </summary>
    private void AllocateLargestStockFirst(CartProduct cp, List<AllocationResult> results)
    {
        int remaining = cp.QuantityNeeded;
        var sorted = cp.SiblingListings.OrderByDescending(s => s.stock).ToList();

        foreach (var (listing, stock) in sorted)
        {
            if (remaining <= 0) break;

            int allocate = Math.Min(remaining, stock);
            results.Add(new AllocationResult
            {
                Listing = listing,
                AllocatedQuantity = allocate,
                UnitPrice = cp.UnitPrice,
                Title = cp.Title,
                Image = cp.Image
            });

            remaining -= allocate;

            _logger.LogInformation(
                "LargestStockFallback: Allocated {Qty} of '{Title}' from Branch {BranchId} (stock: {Stock})",
                allocate, cp.Title, listing.BranchId, stock);
        }

        if (remaining > 0)
        {
            throw new BadRequestException(
                $"Insufficient stock for '{cp.Title}'. Still need {remaining} more.");
        }
    }

    // ── Internal helper classes ──

    private class CartProduct
    {
        public string Title { get; set; } = "";
        public string UnitPrice { get; set; } = "0";
        public string? Image { get; set; }
        public int QuantityNeeded { get; set; }
        public List<(ProductListing listing, int stock)> SiblingListings { get; set; } = new();
    }

    private class BranchCapability
    {
        public Guid BranchId { get; set; }
        public List<string> CanFulfill { get; set; } = new();
        public int TotalStock { get; set; }
    }
}
