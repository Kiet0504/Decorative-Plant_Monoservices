using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Inventory.Commands;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Application.Features.Inventory.Handlers;

public class PublishBatchToStockCommandHandler : IRequestHandler<PublishBatchToStockCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<PublishBatchToStockCommandHandler> _logger;

    public PublishBatchToStockCommandHandler(IApplicationDbContext context, ILogger<PublishBatchToStockCommandHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<bool> Handle(PublishBatchToStockCommand request, CancellationToken ct)
    {
        var batch = await _context.PlantBatches
            .Include(x => x.Taxonomy)
            .Include(x => x.Branch)
            .FirstOrDefaultAsync(x => x.Id == request.BatchId, ct)
            ?? throw new NotFoundException($"Batch {request.BatchId} not found.");

        if (batch.BranchId == null)
            throw new BadRequestException("Batch must be assigned to a branch before publishing to sales.");

        if (batch.Specs != null && batch.Specs.RootElement.TryGetProperty("health_status", out var healthProp))
        {
            var health = healthProp.GetString()?.ToLower();
            if (health != "healthy")
                throw new BadRequestException($"Only healthy plants can be sent to sales. Current status: {health}");
        }

        if (request.Quantity <= 0)
            throw new BadRequestException("Quantity must be greater than zero.");

        if (batch.CurrentTotalQuantity < request.Quantity)
            throw new BadRequestException($"Insufficient quantity in batch. Available: {batch.CurrentTotalQuantity}");

        // 1. Find or create a 'Sales' location for this branch
        var location = await _context.InventoryLocations
            .FirstOrDefaultAsync(x => x.BranchId == batch.BranchId && (x.Type == "Sales" || x.Type == "Storefront"), ct);

        if (location == null)
        {
            location = new InventoryLocation
            {
                Id = Guid.NewGuid(),
                BranchId = batch.BranchId,
                Name = "Main Sales Floor",
                Type = "Sales",
                Code = $"SALES-{batch.BranchId.Value.ToString().Substring(0, 4).ToUpper()}",
                Details = JsonDocument.Parse(JsonSerializer.Serialize(new { description = "Automatically created sales area" }))
            };
            _context.InventoryLocations.Add(location);
            _logger.LogInformation("Created default sales location for branch {BranchId}", batch.BranchId);
        }

        // 2. Decrement Batch Quantity (Moving plants out of cultivation)
        batch.CurrentTotalQuantity -= request.Quantity;

        // 3. CONSOLIDATION LOGIC: Find all stock records for this batch at this branch
        var allStocks = await _context.BatchStocks
            .Include(s => s.Location)
            .Where(x => x.BatchId == batch.Id && x.Location.BranchId == batch.BranchId)
            .ToListAsync(ct);

        int totalAvailable = 0;
        int totalReserved = 0;
        int totalReceived = 0;

        // Extract and sum current quantities from all existing records
        foreach (var s in allStocks)
        {
            if (s.Quantities != null)
            {
                var root = s.Quantities.RootElement;
                if (root.TryGetProperty("available_quantity", out var aq)) totalAvailable += aq.GetInt32();
                if (root.TryGetProperty("reserved_quantity", out var rq)) totalReserved += rq.GetInt32();
                if (root.TryGetProperty("total_received", out var tr)) totalReceived += tr.GetInt32();
                // Removed quantity fallback to prevent double counting initial batch
            }
        }

        // Apply the transfer: move requested quantity from Reserved to Available
        int moveAmount = Math.Min(request.Quantity, totalReserved);
        // Note: we already checked batch.CurrentTotalQuantity, which is the master source of Reserved, 
        // but here we sync within the records themselves.
        
        totalAvailable += request.Quantity;
        totalReserved = Math.Max(0, totalReserved - request.Quantity);
        totalReceived += request.Quantity;

        var quantitiesJson = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            quantity = totalAvailable + totalReserved, // Total items currently at branch
            available_quantity = totalAvailable,
            reserved_quantity = totalReserved,
            total_received = totalReceived
        }));

        // Upsert into a SINGLE record at the Sales location
        var targetStock = allStocks.FirstOrDefault(s => s.LocationId == location.Id) 
                          ?? allStocks.FirstOrDefault(); // Prefer sales location, otherwise reuse any

        if (targetStock == null)
        {
            targetStock = new BatchStock
            {
                Id = Guid.NewGuid(),
                BatchId = batch.Id,
                LocationId = location.Id,
                Quantities = quantitiesJson,
                HealthStatus = "Healthy",
                UpdatedAt = DateTime.UtcNow
            };
            _context.BatchStocks.Add(targetStock);
        }
        else
        {
            targetStock.LocationId = location.Id; // Ensure it moves to the sales location
            targetStock.Quantities = quantitiesJson;
            targetStock.UpdatedAt = DateTime.UtcNow;
        }

        // Remove any other records for this batch at this branch to enforce consolidation
        var extraStocks = allStocks.Where(s => s.Id != targetStock.Id).ToList();
        if (extraStocks.Any())
        {
            _context.BatchStocks.RemoveRange(extraStocks);
            _logger.LogInformation("Consolidated {Count} stock records for batch {BatchId} at branch {BranchId}", extraStocks.Count + 1, batch.Id, batch.BranchId);
        }

        // 4. Ensure a ProductListing exists for this species at this branch
        var existingListing = await _context.ProductListings
            .FirstOrDefaultAsync(x => x.BranchId == batch.BranchId && x.BatchId == batch.Id, ct);

        string targetTitle = $"{(batch.Taxonomy?.CommonNames?.RootElement.TryGetProperty("en", out var enName) == true ? enName.GetString() : batch.Taxonomy?.ScientificName)}";

        if (existingListing == null)
        {
            // Create a DRAFT listing
            var listing = new ProductListing
            {
                Id = Guid.NewGuid(),
                BranchId = batch.BranchId,
                BatchId = batch.Id,
                CreatedAt = DateTime.UtcNow,
                ProductInfo = JsonDocument.Parse(JsonSerializer.Serialize(new
                {
                    title = targetTitle,
                    scientific_name = batch.Taxonomy?.ScientificName,
                    slug = $"batch-{batch.BatchCode?.ToLower() ?? batch.Id.ToString().Substring(0, 8)}",
                    description = "New stock arrival. Please update details.",
                    price = "0", // Default
                    stock_quantity = totalAvailable,
                    min_order = 1,
                    max_order = 10
                })),
                StatusInfo = JsonDocument.Parse(JsonSerializer.Serialize(new
                {
                    status = "active",
                    visibility = "public",
                    featured = false,
                    view_count = 0,
                    sold_count = 0,
                    tags = new List<string>()
                }))
            };
            _context.ProductListings.Add(listing);
            _logger.LogInformation("Created draft ProductListing for batch {BatchId}", batch.Id);
        }
        else
        {
            // FORCE UPDATE THE TITLE if it's currently wrong or from old logic
            var info = JsonSerializer.Deserialize<Dictionary<string, object>>(existingListing.ProductInfo!.RootElement.GetRawText())!;
            info["title"] = targetTitle;
            info["stock_quantity"] = totalAvailable; // Also sync stock quantity in listing info
            existingListing.ProductInfo = JsonDocument.Parse(JsonSerializer.Serialize(info));
            _logger.LogInformation("Updated existing ProductListing title/stock for batch {BatchId}", batch.Id);
        }

        await _context.SaveChangesAsync(ct);
        return true;
    }
}
