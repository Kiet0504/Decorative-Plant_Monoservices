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

        // 1. Decrement Master Batch Quantity (Moving plants out of cultivation)
        batch.CurrentTotalQuantity -= request.Quantity;

        // 2. Find all stock records for this batch at this branch
        var allStocks = await _context.BatchStocks
            .Include(s => s.Location)
            .Where(x => x.BatchId == batch.Id && x.Location!.BranchId == batch.BranchId)
            .ToListAsync(ct);

        // 3. Update stock records: Convert Reserved to Available within Cultivation Stocks
        var cultivationStocksQuery = allStocks
            .Where(s => s.Location?.Type != "Sales" && s.Location?.Type != "Storefront");

        if (request.SourceLocationId.HasValue)
        {
            cultivationStocksQuery = cultivationStocksQuery
                .OrderByDescending(s => s.LocationId == request.SourceLocationId.Value);
        }
        else
        {
            cultivationStocksQuery = cultivationStocksQuery
                .OrderByDescending(s => {
                    if (s.Quantities == null) return 0;
                    if (s.Quantities.RootElement.TryGetProperty("reserved_quantity", out var rq)) return rq.GetInt32();
                    return 0;
                });
        }

        var cultivationStocks = cultivationStocksQuery.ToList();

        // Convert reserved to available
        int remainingToPublish = request.Quantity;
        foreach (var sourceStock in cultivationStocks)
        {
            if (remainingToPublish <= 0) break;

            int sInitial = 0;
            int sAvailable = 0;
            int sReserved = 0;
            int sReceived = 0;
            
            if (sourceStock.Quantities != null)
            {
                var root = sourceStock.Quantities.RootElement;
                if (root.TryGetProperty("quantity", out var q) && q.ValueKind == JsonValueKind.Number) sInitial = (int)q.GetDouble();
                if (root.TryGetProperty("available_quantity", out var aq) && aq.ValueKind == JsonValueKind.Number) sAvailable = (int)aq.GetDouble();
                if (root.TryGetProperty("reserved_quantity", out var rq) && rq.ValueKind == JsonValueKind.Number) sReserved = (int)rq.GetDouble();
                if (root.TryGetProperty("total_received", out var tr) && tr.ValueKind == JsonValueKind.Number) sReceived = (int)tr.GetDouble();
            }

            int canTake = Math.Min(remainingToPublish, sReserved);
            
            sReserved -= canTake;
            sAvailable += canTake;
            sReceived += canTake; 

            sourceStock.Quantities = JsonDocument.Parse(JsonSerializer.Serialize(new
            {
                quantity = sInitial,
                total_received = sReceived,
                reserved_quantity = sReserved,
                available_quantity = sAvailable
            }));
            sourceStock.UpdatedAt = DateTime.UtcNow;
            
            remainingToPublish -= canTake;
        }

        if (remainingToPublish > 0)
        {
            throw new BadRequestException($"Could not find enough reserved plants to publish. Missing: {remainingToPublish}");
        }

        // 4. Calculate total available for ProductListing update (Across ALL batches of this species at this branch)
        int totalAvailable = await _context.BatchStocks
            .Include(s => s.Batch)
            .Include(s => s.Location)
            .Where(s => s.Batch!.TaxonomyId == batch.TaxonomyId && s.Location!.BranchId == batch.BranchId)
            .ToListAsync(ct)
            .ContinueWith(t => t.Result.Sum(s => {
                if (s.Quantities == null) return 0;
                if (s.Quantities.RootElement.TryGetProperty("available_quantity", out var aq) && aq.ValueKind == JsonValueKind.Number) return (int)aq.GetDouble();
                return 0;
            }));

        // 5. Ensure a ProductListing exists for this species at this branch
        var existingListing = await _context.ProductListings
            .Include(x => x.Batch)
            .FirstOrDefaultAsync(x => x.BranchId == batch.BranchId && x.Batch!.TaxonomyId == batch.TaxonomyId, ct);

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
                    price = "0", 
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
            
            info["stock_quantity"] = totalAvailable;
            
            if (request.Price != null)
            {
               info["price"] = request.Price;
            }

            if (info.ContainsKey("description") && info["description"]?.ToString() == "New stock arrival. Please update details.")
            {
                string taxonomyDesc = batch.Taxonomy?.TaxonomyInfo?.RootElement.TryGetProperty("description", out var descProp) == true ? descProp.GetString() ?? "" : "";
                if (!string.IsNullOrEmpty(taxonomyDesc)) info["description"] = taxonomyDesc;
            }

            if (!info.ContainsKey("care_info") || info["care_info"] == null)
            {
                if (batch.Taxonomy?.CareInfo != null) info["care_info"] = batch.Taxonomy.CareInfo;
            }
            if (!info.ContainsKey("growth_info") || info["growth_info"] == null)
            {
                if (batch.Taxonomy?.GrowthInfo != null) info["growth_info"] = batch.Taxonomy.GrowthInfo;
            }

            existingListing.ProductInfo = JsonDocument.Parse(JsonSerializer.Serialize(info));

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

        // 6. Send notification to all Admins
        try
        {
            var adminEmails = await _context.UserAccounts
                .Where(u => u.Role == "admin" && u.IsActive && !string.IsNullOrEmpty(u.Email))
                .Select(u => u.Email)
                .ToListAsync(ct);

            if (adminEmails.Any())
            {
                var subject = $"New Plants Sent to Sales: {batch.BatchCode}";
                
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
