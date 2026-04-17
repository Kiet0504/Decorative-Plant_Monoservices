using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Inventory.Commands;
using decorativeplant_be.Domain.Entities;

using decorativeplant_be.Application.Services;
using decorativeplant_be.Application.Common.DTOs.Email;

namespace decorativeplant_be.Application.Features.Inventory.Handlers;

public class PublishBatchToStockCommandHandler : IRequestHandler<PublishBatchToStockCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<PublishBatchToStockCommandHandler> _logger;
    private readonly IEmailService _emailService;

    public PublishBatchToStockCommandHandler(
        IApplicationDbContext context, 
        ILogger<PublishBatchToStockCommandHandler> logger,
        IEmailService emailService)
    {
        _context = context;
        _logger = logger;
        _emailService = emailService;
    }

    public async Task<bool> Handle(PublishBatchToStockCommand request, CancellationToken ct)
    {
        var batch = await _context.PlantBatches
            .Include(x => x.Taxonomy)
                .ThenInclude(t => t!.Category)
            .Include(x => x.Branch)
            .Include(x => x.Supplier)
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
            .Where(x => x.BatchId == batch.Id && x.Location!.BranchId == batch.BranchId)
            .ToListAsync(ct);

        int totalAvailable = 0;
        int totalReserved = 0;
        int totalReceived = 0;

        foreach (var s in allStocks)
        {
            if (s.Quantities != null)
            {
                var root = s.Quantities.RootElement;
                if (root.TryGetProperty("available_quantity", out var aq)) totalAvailable += aq.GetInt32();
                if (root.TryGetProperty("reserved_quantity", out var rq)) totalReserved += rq.GetInt32();
                if (root.TryGetProperty("total_received", out var tr)) totalReceived += tr.GetInt32();
            }
        }

        totalAvailable += request.Quantity;
        totalReserved = Math.Max(0, totalReserved - request.Quantity);
        totalReceived += request.Quantity;

        var quantitiesJson = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            quantity = totalAvailable + totalReserved, 
            available_quantity = totalAvailable,
            reserved_quantity = totalReserved,
            total_received = totalReceived
        }));

        var targetStock = allStocks.FirstOrDefault(s => s.LocationId == location.Id) 
                          ?? allStocks.FirstOrDefault(); 

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
            targetStock.LocationId = location.Id; 
            targetStock.Quantities = quantitiesJson;
            targetStock.UpdatedAt = DateTime.UtcNow;
        }

        var extraStocks = allStocks.Where(s => s.Id != targetStock.Id).ToList();
        if (extraStocks.Any())
        {
            _context.BatchStocks.RemoveRange(extraStocks);
        }

        // 4. Ensure a ProductListing exists for this species at this branch
        var existingListing = await _context.ProductListings
            .FirstOrDefaultAsync(x => x.BranchId == batch.BranchId && x.BatchId == batch.Id, ct);

        string taxonomyTitleVi = batch.Taxonomy?.CommonNames?.RootElement.TryGetProperty("vi", out var viName) == true ? viName.GetString() ?? "" : "";
        string taxonomyTitleEn = batch.Taxonomy?.CommonNames?.RootElement.TryGetProperty("en", out var enName) == true ? enName.GetString() ?? "" : "";
        string targetTitle = !string.IsNullOrEmpty(taxonomyTitleVi) ? taxonomyTitleVi : (!string.IsNullOrEmpty(taxonomyTitleEn) ? taxonomyTitleEn : (batch.Taxonomy?.ScientificName ?? "Untitled Plant"));

        if (existingListing == null)
        {
            string taxonomyDesc = batch.Taxonomy?.TaxonomyInfo?.RootElement.TryGetProperty("description", out var descProp) == true ? descProp.GetString() ?? "" : "New stock arrival. Please update details.";
            
            var images = new List<object>();
            if (!string.IsNullOrEmpty(batch.Taxonomy?.ImageUrl))
            {
                images.Add(new
                {
                    url = batch.Taxonomy.ImageUrl,
                    alt = targetTitle,
                    is_primary = true,
                    sort_order = 0
                });
            }

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
                    description = taxonomyDesc,
                    price = batch.Taxonomy?.DefaultPrice?.ToString("0") ?? "0", 
                    stock_quantity = totalAvailable,
                    min_order = 1,
                    max_order = 10,
                    care_info = batch.Taxonomy?.CareInfo,   
                    growth_info = batch.Taxonomy?.GrowthInfo
                })),
                StatusInfo = JsonDocument.Parse(JsonSerializer.Serialize(new
                {
                    status = "active",
                    visibility = "public",
                    featured = false,
                    view_count = 0,
                    sold_count = 0,
                    tags = new List<string> { batch.Taxonomy?.Category?.Name ?? "Uncategorized" }
                })),
                Images = JsonDocument.Parse(JsonSerializer.Serialize(images))
            };
            _context.ProductListings.Add(listing);
            _logger.LogInformation("Created ProductListing for batch {BatchId} using Taxonomy data", batch.Id);
        }
        else
        {
            // Upgrade existing listing if it's using placeholder data
            var info = JsonSerializer.Deserialize<Dictionary<string, object>>(existingListing.ProductInfo!.RootElement.GetRawText())!;
            
            // 1. Sync Title & Stock (Always)
            info["title"] = targetTitle;
            info["stock_quantity"] = totalAvailable;

            // 2. Backfill Description if placeholder
            if (!info.ContainsKey("description") || info["description"]?.ToString() == "New stock arrival. Please update details.")
            {
                string taxonomyDesc = batch.Taxonomy?.TaxonomyInfo?.RootElement.TryGetProperty("description", out var descProp) == true ? descProp.GetString() ?? "" : "";
                if (!string.IsNullOrEmpty(taxonomyDesc)) info["description"] = taxonomyDesc;
            }

            // 3. Backfill Care & Growth Info if missing
            if (!info.ContainsKey("care_info") || info["care_info"] == null)
            {
                if (batch.Taxonomy?.CareInfo != null) info["care_info"] = batch.Taxonomy.CareInfo;
            }
            if (!info.ContainsKey("growth_info") || info["growth_info"] == null)
            {
                if (batch.Taxonomy?.GrowthInfo != null) info["growth_info"] = batch.Taxonomy.GrowthInfo;
            }

            // 4. Sync Price if taxonomy has a default price
            if (batch.Taxonomy?.DefaultPrice.HasValue == true)
            {
                info["price"] = batch.Taxonomy.DefaultPrice.Value.ToString("0");
            }

            existingListing.ProductInfo = JsonDocument.Parse(JsonSerializer.Serialize(info));

            // 4. Backfill Images if empty
            if (existingListing.Images == null || existingListing.Images.RootElement.ValueKind != JsonValueKind.Array || existingListing.Images.RootElement.GetArrayLength() == 0)
            {
                if (!string.IsNullOrEmpty(batch.Taxonomy?.ImageUrl))
                {
                    var images = new List<object> {
                        new { url = batch.Taxonomy.ImageUrl, alt = targetTitle, is_primary = true, sort_order = 0 }
                    };
                    existingListing.Images = JsonDocument.Parse(JsonSerializer.Serialize(images));
                }
            }

            _logger.LogInformation("Updated existing ProductListing {ListingId} with stock and potential taxonomy backfill", existingListing.Id);
        }

        // 5. Send notification to all Admins
        try
        {
            var adminEmails = await _context.UserAccounts
                .Where(u => u.Role == "admin" && u.IsActive && !string.IsNullOrEmpty(u.Email))
                .Select(u => u.Email)
                .ToListAsync(ct);

            if (adminEmails.Any())
            {
                var subject = $"New Plants Sent to Sales: {batch.BatchCode}";
                
                // Extract purchase cost from SourceInfo if it exists
                decimal cost = 0;
                if (batch.SourceInfo != null)
                {
                    try { 
                        if (batch.SourceInfo.RootElement.TryGetProperty("purchase_cost", out var costProp))
                        {
                            if (costProp.TryGetDecimal(out var parsedCost)) cost = parsedCost;
                            else if (costProp.ValueKind == JsonValueKind.String && decimal.TryParse(costProp.GetString(), out var strCost)) cost = strCost;
                        }
                    } catch { }
                }

                var batchInfo = new Dictionary<string, string>
                {
                    { "Batch Code", batch.BatchCode ?? "N/A" },
                    { "Species", targetTitle },
                    { "Branch", batch.Branch?.Name ?? "N/A" },
                    { "Supplier", batch.Supplier?.Name ?? "N/A" },
                    { "Quantity", request.Quantity.ToString() },
                    { "Purchase Cost", cost == 0 ? "Not declared" : cost.ToString("N0") + " VND" },
                    { "Timestamp", DateTime.Now.ToString("MM/dd/yyyy HH:mm") }
                };

                var emailBody = $@"
                    <h2>New Finished Plants Dispatched to Sales</h2>
                    <p>Dear Admin, a cultivation batch has just been finished and dispatched to the sales floor. Please review the costs to establish the appropriate retail price.</p>
                    <table style='width:100%; border-collapse: collapse;'>
                        {string.Join("", batchInfo.Select(x => $@"
                            <tr style='border-bottom: 1px solid #eee;'>
                                <td style='padding: 10px; font-weight: bold; width: 250px;'>{x.Key}</td>
                                <td style='padding: 10px;'>{x.Value}</td>
                            </tr>
                        "))}
                    </table>
                    <p style='margin-top: 20px; color: #666;'>This is an automated email from the Decorative Plant Management System.</p>";

                foreach (var email in adminEmails)
                {
                    await _emailService.SendAsync(new EmailMessage
                    {
                        To = email,
                        Subject = subject,
                        BodyHtml = emailBody,
                        BodyPlainText = $"New Finished Plants: {batch.BatchCode} - {targetTitle}. Branch: {batch.Branch?.Name}. Purchase Cost: {cost}. Please review the admin dashboard."
                    }, ct);
                }
                
                _logger.LogInformation("Admin notification email sent to {Count} admins for batch {BatchId}", adminEmails.Count, batch.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send admin notification email for batch {BatchId}", batch.Id);
        }

        await _context.SaveChangesAsync(ct);
        return true;
    }
}
