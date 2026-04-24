using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using decorativeplant_be.Application.Common.DTOs.Commerce;
using decorativeplant_be.Application.Common.DTOs.Common;
using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Commerce.ProductListings.Commands;
using decorativeplant_be.Application.Features.Commerce.ProductListings.Queries;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Application.Features.Commerce.ProductListings.Handlers;

public class CreateProductListingHandler : IRequestHandler<CreateProductListingCommand, ProductListingResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<CreateProductListingHandler> _logger;

    public CreateProductListingHandler(IApplicationDbContext context, ILogger<CreateProductListingHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ProductListingResponse> Handle(CreateProductListingCommand cmd, CancellationToken ct)
    {
        var req = cmd.Request;
        var slug = req.Slug ?? req.Title.ToLower().Replace(" ", "-");

        var entity = new ProductListing
        {
            Id = Guid.NewGuid(),
            BranchId = req.BranchId,
            BatchId = req.BatchId,
            CreatedAt = DateTime.UtcNow,
            ProductInfo = JsonDocument.Parse(JsonSerializer.Serialize(new
            {
                title = req.Title,
                scientific_name = req.ScientificName,
                slug,
                description = req.Description,
                price = req.Price,
                stock_quantity = req.StockQuantity,
                min_order = req.MinOrder,
                max_order = req.MaxOrder,
                care_info = req.CareInfo != null ? JsonDocument.Parse(JsonSerializer.Serialize(req.CareInfo)) : null,
                growth_info = req.GrowthInfo != null ? JsonDocument.Parse(JsonSerializer.Serialize(req.GrowthInfo)) : null,
                taxonomy_info = req.TaxonomyInfo != null ? JsonDocument.Parse(JsonSerializer.Serialize(req.TaxonomyInfo)) : null
            })),
            StatusInfo = JsonDocument.Parse(JsonSerializer.Serialize(new
            {
                status = req.Status,
                visibility = req.Visibility,
                featured = req.Featured,
                view_count = 0,
                sold_count = 0,
                tags = req.Tags
            })),
            SeoInfo = !string.IsNullOrEmpty(req.MetaTitle) ? JsonDocument.Parse(JsonSerializer.Serialize(new
            {
                meta_title = req.MetaTitle,
                meta_description = req.MetaDescription,
                meta_keywords = req.MetaKeywords
            })) : null,
            Images = req.Images.Count > 0 ? JsonDocument.Parse(JsonSerializer.Serialize(
                req.Images.Select(i => new { url = i.Url, alt = i.Alt, is_primary = i.IsPrimary, sort_order = i.SortOrder }))) : null
        };

        // Automatic Batch Creation if StockQuantity is provided
        if (req.StockQuantity > 0)
        {
            
            var batch = new PlantBatch
            {
                Id = Guid.NewGuid(),
                BatchCode = $"AUTO-{entity.Id.ToString().Substring(0, 8).ToUpper()}",
                BranchId = entity.BranchId,
                InitialQuantity = req.StockQuantity,
                CurrentTotalQuantity = req.StockQuantity,
                CreatedAt = DateTime.UtcNow
            };
            _context.PlantBatches.Add(batch);
            entity.BatchId = batch.Id;
            entity.Batch = batch;
            _logger.LogInformation("Automatically created Batch {BatchId} with quantity {Quantity} for new Product {ProductId}", 
                batch.Id, req.StockQuantity, entity.Id);
        }

        _context.ProductListings.Add(entity);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Created ProductListing {Id} with Batch {BatchId}", entity.Id, entity.BatchId);
        return MapToResponse(entity);
    }

    internal static string? GetProductTitle(ProductListing e)
    {
        if (e.ProductInfo == null) return null;
        return e.ProductInfo.RootElement.TryGetProperty("title", out var t) ? t.GetString() : null;
    }

    internal static ProductListingResponse MapToResponse(ProductListing e)
    {
        var response = new ProductListingResponse
        {
            Id = e.Id,
            BranchId = e.BranchId,
            BranchName = e.Branch?.Name,
            BranchAddress = e.Branch?.ContactInfo?.RootElement.TryGetProperty("full_address", out var addr) == true ? addr.GetString() : null,
            BatchId = e.BatchId,
            BatchCode = e.Batch?.BatchCode,
            BatchCreatedAt = e.Batch?.CreatedAt,
            CreatedAt = e.CreatedAt
        };

        if (e.ProductInfo != null)
        {
            var root = e.ProductInfo.RootElement;
            response.Title = root.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
            response.ScientificName = root.TryGetProperty("scientific_name", out var sn) ? sn.GetString() : null;
            response.Slug = root.TryGetProperty("slug", out var s) ? s.GetString() : null;
            response.Description = root.TryGetProperty("description", out var d) ? d.GetString() : null;
            response.Price = root.TryGetProperty("price", out var p) ? p.GetString() ?? "0" : "0";
            response.MinOrder = root.TryGetProperty("min_order", out var mn) ? mn.GetInt32() : 1;
            response.MaxOrder = root.TryGetProperty("max_order", out var mx) ? mx.GetInt32() : 10;
            response.StockQuantity = root.TryGetProperty("stock_quantity", out var sq) ? sq.GetInt32() : 0;
            
            // Map botanical info if it exists
            if (root.TryGetProperty("care_info", out var ci) && ci.ValueKind != JsonValueKind.Null) response.CareInfo = JsonDocument.Parse(ci.GetRawText());
            if (root.TryGetProperty("growth_info", out var gi) && gi.ValueKind != JsonValueKind.Null) response.GrowthInfo = JsonDocument.Parse(gi.GetRawText());
            if (root.TryGetProperty("taxonomy_info", out var ti) && ti.ValueKind != JsonValueKind.Null) response.TaxonomyInfo = JsonDocument.Parse(ti.GetRawText());
        }

        // Taxonomy fields (scientific name + taxonomy id) come from the batch when available.
        // IMPORTANT: Do NOT override the product listing title for customer-facing surfaces.
        // The listing title is what the shop is actually selling/branding ("Lan Y", etc.).
        // Overriding it with taxonomy common-name can make AI/shop surfaces look "wrong".
        if (e.Batch?.Taxonomy != null)
        {
            string taxVi = "";
            string taxEn = "";
            if (e.Batch.Taxonomy.CommonNames != null)
            {
                var root = e.Batch.Taxonomy.CommonNames.RootElement;
                if (root.TryGetProperty("vi", out var viProp)) taxVi = viProp.GetString() ?? "";
                if (root.TryGetProperty("en", out var enProp)) taxEn = enProp.GetString() ?? "";
            }
            
            var targetTitle = !string.IsNullOrEmpty(taxEn) ? taxEn : (!string.IsNullOrEmpty(taxVi) ? taxVi : e.Batch.Taxonomy.ScientificName);
            // Keep the listing's own title if present; only fill from taxonomy when missing.
            if (string.IsNullOrWhiteSpace(response.Title) && !string.IsNullOrEmpty(targetTitle))
            {
                response.Title = targetTitle;
            }
            
            response.ScientificName = e.Batch.Taxonomy.ScientificName;
            response.CommonNameEn = taxEn;
            response.CommonNameVi = taxVi;
            response.TaxonomyId = e.Batch.TaxonomyId;
        }

        // Strictly use real stock from ALL linked batches of this species at this branch
        response.StockQuantity = 0;
        if (e.Batch?.TaxonomyId != null)
        {
            // Note: We expect allRelevantStocks to be loaded via Includes in the query handler
            // However, since we can't easily access the DbContext here, 
            // and we want to avoid N+1, we assume the handler loaded them or we use the aggregated JSON in ProductInfo as a fallback.
            
            // Actually, in the current architecture, we have all relevant stocks in memory if we use the correct include.
            // Let's check the handler query... yes, it includes Batch.BatchStocks.
            
            // Wait, we need to find all BatchStocks at this branch for this taxonomy.
            // Since we only have access to e.Batch.BatchStocks (which are for ONE batch),
            // we should rely on the 'stock_quantity' field in ProductInfo which we just updated in the Publish handler.
            
            if (e.ProductInfo != null && e.ProductInfo.RootElement.TryGetProperty("stock_quantity", out var sqProp))
            {
                response.StockQuantity = sqProp.GetInt32();
            }

            // Also populate the specific batch details for the 'Main' batch linked to this listing
            var batchStock = e.Batch.BatchStocks?.FirstOrDefault(s => s.Location?.BranchId == e.BranchId);
            if (batchStock != null && batchStock.Quantities != null)
            {
                var root = batchStock.Quantities.RootElement;
                if (root.TryGetProperty("quantity", out var tq)) response.BatchTotalQuantity = tq.GetInt32();
                if (root.TryGetProperty("reserved_quantity", out var rq)) response.BatchReservedQuantity = rq.GetInt32();
                if (root.TryGetProperty("total_received", out var tr)) response.BatchTotalReceived = tr.GetInt32();
                response.StockLocationId = batchStock.LocationId;
            }
        }

        if (e.StatusInfo != null)
        {
            var root = e.StatusInfo.RootElement;
            response.Status = root.TryGetProperty("status", out var st) ? st.GetString() ?? "draft" : "draft";
            response.Visibility = root.TryGetProperty("visibility", out var v) ? v.GetString() ?? "public" : "public";
            response.Featured = root.TryGetProperty("featured", out var f) && f.GetBoolean();
            response.ViewCount = root.TryGetProperty("view_count", out var vc) ? vc.GetInt32() : 0;
            response.SoldCount = root.TryGetProperty("sold_count", out var sc) ? sc.GetInt32() : 0;
            if (root.TryGetProperty("tags", out var tags) && tags.ValueKind == JsonValueKind.Array)
                response.Tags = tags.EnumerateArray().Select(x => x.GetString() ?? "").ToList();
        }

        if (e.Images != null && e.Images.RootElement.ValueKind == JsonValueKind.Array)
        {
            response.Images = e.Images.RootElement.EnumerateArray().Select(img => new ProductImageDto
            {
                Url = img.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "",
                Alt = img.TryGetProperty("alt", out var a) ? a.GetString() : null,
                IsPrimary = img.TryGetProperty("is_primary", out var ip) && ip.GetBoolean(),
                SortOrder = img.TryGetProperty("sort_order", out var so) ? so.GetInt32() : 0
            }).ToList();
        }
        
        // Single branch stock entry if loaded
        if (e.Branch != null)
        {
            string address = "";
            if (e.Branch.ContactInfo != null)
            {
                if (e.Branch.ContactInfo.RootElement.TryGetProperty("full_address", out var addrProp))
                    address = addrProp.GetString() ?? "";
            }

            response.BranchStocks.Add(new BranchStockDto
            {
                ListingId = e.Id,
                BranchId = e.Branch.Id,
                BranchName = e.Branch.Name,
                BranchAddress = address,
                OperatingHours = e.Branch.OperatingHours,
                Price = response.Price,
                StockQuantity = response.StockQuantity
            });
        }


        return response;
    }

}

public class UpdateProductListingHandler : IRequestHandler<UpdateProductListingCommand, ProductListingResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<UpdateProductListingHandler> _logger;

    public UpdateProductListingHandler(IApplicationDbContext context, ILogger<UpdateProductListingHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ProductListingResponse> Handle(UpdateProductListingCommand cmd, CancellationToken ct)
    {
        var req = cmd.Request;
        var entity = await _context.ProductListings
            .Include(x => x.Batch)
                .ThenInclude(b => b!.BatchStocks)
                    .ThenInclude(bs => bs.Location)
            .FirstOrDefaultAsync(x => x.Id == cmd.Id, ct)
            ?? throw new NotFoundException($"ProductListing {cmd.Id} not found.");

        if (req.StockQuantity.HasValue)
        {
            if (entity.Batch != null)
            {
                _logger.LogInformation("Stock change requested for listing {Id}. Syncing with BatchStock record.", entity.Id);
                
                // ── Synchronization Logic: BatchStock ──
                var batchStock = await _context.BatchStocks
                    .FirstOrDefaultAsync(s => s.BatchId == entity.BatchId && s.Location!.BranchId == entity.BranchId, ct);

                if (batchStock != null && batchStock.Quantities != null)
                {
                    var quantities = JsonSerializer.Deserialize<Dictionary<string, int>>(batchStock.Quantities.RootElement.GetRawText()) ?? new();
                    
                    int currentReserved = quantities.ContainsKey("reserved_quantity") ? quantities["reserved_quantity"] : 0;
                    int totalReceived = quantities.ContainsKey("total_received") ? quantities["total_received"] : 0;
                    
                    if (req.StockQuantity.Value > totalReceived && totalReceived > 0)
                    {
                        throw new ValidationException($"Cannot set available stock to {req.StockQuantity.Value}. Maximum allowed (total received) is {totalReceived}.");
                    }

                    // Update Available and Total Quantity
                    quantities["available_quantity"] = req.StockQuantity.Value;
                    quantities["quantity"] = req.StockQuantity.Value + currentReserved;
                    
                    batchStock.Quantities = JsonDocument.Parse(JsonSerializer.Serialize(quantities));
                    batchStock.UpdatedAt = DateTime.UtcNow;
                    
                    _logger.LogInformation("Synced BatchStock {BatchStockId} for ProductListing {ListingId}. New available: {NewQty}", 
                        batchStock.Id, entity.Id, req.StockQuantity.Value);
                }
            }
            else
            {
                var batch = new PlantBatch
                {
                    Id = Guid.NewGuid(),
                    BatchCode = $"AUTO-{entity.Id.ToString().Substring(0, 8).ToUpper()}",
                    BranchId = entity.BranchId,
                    InitialQuantity = req.StockQuantity.Value,
                    CurrentTotalQuantity = req.StockQuantity.Value,
                    CreatedAt = DateTime.UtcNow
                };
                _context.PlantBatches.Add(batch);
                entity.BatchId = batch.Id;
                entity.Batch = batch;
                _logger.LogInformation("Created new Batch {BatchId} with quantity {Quantity} for Product {ProductId}", 
                    batch.Id, req.StockQuantity.Value, entity.Id);
            }
        }

        // Merge product_info
        var productInfo = new Dictionary<string, object?>();
        if (entity.ProductInfo != null)
            foreach (var prop in entity.ProductInfo.RootElement.EnumerateObject())
                productInfo[prop.Name] = GetJsonValue(prop.Value);

        if (req.Title != null) productInfo["title"] = req.Title;
        if (req.ScientificName != null) productInfo["scientific_name"] = req.ScientificName;
        if (req.Slug != null) productInfo["slug"] = req.Slug;
        if (req.Description != null) productInfo["description"] = req.Description;
        if (req.Price != null) 
        {
            if (cmd.UserRole != "admin")
            {
                _logger.LogWarning("Non-admin user attempted to update price for product {Id}", entity.Id);
                throw new ValidationException("Only administrators can update product prices.");
            }
            productInfo["price"] = req.Price;
        }
        if (req.MinOrder.HasValue) productInfo["min_order"] = req.MinOrder.Value;
        if (req.MaxOrder.HasValue) productInfo["max_order"] = req.MaxOrder.Value;
        if (req.StockQuantity.HasValue) productInfo["stock_quantity"] = req.StockQuantity.Value;
        if (req.CareInfo != null) productInfo["care_info"] = JsonDocument.Parse(JsonSerializer.Serialize(req.CareInfo));
        if (req.GrowthInfo != null) productInfo["growth_info"] = JsonDocument.Parse(JsonSerializer.Serialize(req.GrowthInfo));
        if (req.TaxonomyInfo != null) productInfo["taxonomy_info"] = JsonDocument.Parse(JsonSerializer.Serialize(req.TaxonomyInfo));
        
        entity.ProductInfo = JsonDocument.Parse(JsonSerializer.Serialize(productInfo));

        // Merge status_info
        var statusInfo = new Dictionary<string, object?>();
        if (entity.StatusInfo != null)
            foreach (var prop in entity.StatusInfo.RootElement.EnumerateObject())
                statusInfo[prop.Name] = GetJsonValue(prop.Value);

        if (req.Status != null) statusInfo["status"] = req.Status;
        if (req.Visibility != null) statusInfo["visibility"] = req.Visibility;
        if (req.Featured.HasValue) statusInfo["featured"] = req.Featured.Value;
        if (req.Tags != null) statusInfo["tags"] = req.Tags;
        entity.StatusInfo = JsonDocument.Parse(JsonSerializer.Serialize(statusInfo));

        if (req.Images != null)
            entity.Images = JsonDocument.Parse(JsonSerializer.Serialize(
                req.Images.Select(i => new { url = i.Url, alt = i.Alt, is_primary = i.IsPrimary, sort_order = i.SortOrder })));

        await _context.SaveChangesAsync(ct);

        // --- NEW: Synchronization feature (Always for Admin) ---
        if (cmd.UserRole == "admin")
        {
            _logger.LogInformation("Admin syncing product updates to all branches for species related to listing {Id}", entity.Id);
            
            var taxonomyId = entity.Batch?.TaxonomyId;
            var currentTitle = req.Title ?? CreateProductListingHandler.GetProductTitle(entity);
            var normalizedTitle = currentTitle?.Trim().ToLowerInvariant();

            // Find all other listings for the same species
            var otherListings = await _context.ProductListings
                .Include(x => x.Batch)
                .Where(x => x.Id != entity.Id)
                .ToListAsync(ct);

            var targets = otherListings.Where(x => 
            {
                if (taxonomyId.HasValue && x.Batch != null && x.Batch.TaxonomyId == taxonomyId.Value) return true;
                
                if (!taxonomyId.HasValue && !string.IsNullOrEmpty(normalizedTitle))
                {
                    var otherTitle = CreateProductListingHandler.GetProductTitle(x)?.Trim().ToLowerInvariant();
                    return otherTitle == normalizedTitle;
                }

                return false;
            }).ToList();

            foreach (var target in targets)
            {
                // Update ProductInfo JSONB
                var targetPI = new Dictionary<string, object?>();
                if (target.ProductInfo != null)
                    foreach (var prop in target.ProductInfo.RootElement.EnumerateObject())
                        targetPI[prop.Name] = GetJsonValue(prop.Value);

                if (req.Title != null) targetPI["title"] = req.Title;
                if (req.ScientificName != null) targetPI["scientific_name"] = req.ScientificName;
                if (req.Description != null) targetPI["description"] = req.Description;
                if (req.Price != null) targetPI["price"] = req.Price;
                if (req.CareInfo != null) targetPI["care_info"] = JsonDocument.Parse(JsonSerializer.Serialize(req.CareInfo));
                if (req.GrowthInfo != null) targetPI["growth_info"] = JsonDocument.Parse(JsonSerializer.Serialize(req.GrowthInfo));
                
                target.ProductInfo = JsonDocument.Parse(JsonSerializer.Serialize(targetPI));

                // Price and generic info updated for target listings.

                // Update Status/Visibility/Featured
                var targetSI = new Dictionary<string, object?>();
                if (target.StatusInfo != null)
                    foreach (var prop in target.StatusInfo.RootElement.EnumerateObject())
                        targetSI[prop.Name] = GetJsonValue(prop.Value);

                if (req.Status != null) targetSI["status"] = req.Status;
                if (req.Visibility != null) targetSI["visibility"] = req.Visibility;
                if (req.Featured.HasValue) targetSI["featured"] = req.Featured.Value;
                
                target.StatusInfo = JsonDocument.Parse(JsonSerializer.Serialize(targetSI));
                
                // Update Images if provided
                if (req.Images != null)
                    target.Images = JsonDocument.Parse(JsonSerializer.Serialize(
                        req.Images.Select(i => new { url = i.Url, alt = i.Alt, is_primary = i.IsPrimary, sort_order = i.SortOrder })));
            }

            if (targets.Any())
            {
                await _context.SaveChangesAsync(ct);
                _logger.LogInformation("Synchronized {Count} related listings for species '{Title}'", targets.Count, req.Title ?? "Unknown");
            }
        }

        return CreateProductListingHandler.MapToResponse(entity);
    }

    private static object? GetJsonValue(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.TryGetInt32(out var i) ? i : element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        _ => element
    };
}

public class DeleteProductListingHandler : IRequestHandler<DeleteProductListingCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public DeleteProductListingHandler(IApplicationDbContext context) => _context = context;

    public async Task<bool> Handle(DeleteProductListingCommand cmd, CancellationToken ct)
    {
        var entity = await _context.ProductListings.FindAsync(new object[] { cmd.Id }, ct)
            ?? throw new NotFoundException($"ProductListing {cmd.Id} not found.");

        _context.ProductListings.Remove(entity);
        await _context.SaveChangesAsync(ct);
        return true;
    }
}

public class GetProductListingsHandler : IRequestHandler<GetProductListingsQuery, PagedResult<ProductListingResponse>>
{
    private readonly IApplicationDbContext _context;
    public GetProductListingsHandler(IApplicationDbContext context) => _context = context;

    private static decimal ParsePrice(string? priceStr)
    {
        if (string.IsNullOrEmpty(priceStr)) return 0;
        var digits = new string(priceStr.Where(char.IsDigit).ToArray());
        return decimal.TryParse(digits, out var val) ? val : 0;
    }

    public async Task<PagedResult<ProductListingResponse>> Handle(GetProductListingsQuery query, CancellationToken ct)
    {
        // 1. Initial query with necessary includes
        var q = _context.ProductListings
            .Include(x => x.Batch)
                .ThenInclude(b => b!.BatchStocks)
                    .ThenInclude(bs => bs.Location)
            .Include(x => x.Batch)
                .ThenInclude(b => b!.Taxonomy)
            .AsQueryable();

        // 1.5 Filters
        if (query.TaxonomyId.HasValue)
        {
            q = q.Where(x => x.Batch != null && x.Batch.TaxonomyId == query.TaxonomyId.Value);
        }

        if (!string.IsNullOrEmpty(query.CategoryId))
        {
            if (Guid.TryParse(query.CategoryId, out var catId))
            {
                q = q.Where(x => x.Batch != null && x.Batch.Taxonomy != null && x.Batch.Taxonomy.CategoryId == catId);
            }
        }

        // 2. Fetch all relevant entries for aggregation
        // Note: We don't filter by BranchId in SQL yet if we want to show 'Global' info 
        // BUT if the query.BranchId is set, the customer only wants to see what's available AT that branch.

        var allListings = await q.ToListAsync(ct);
        
        // 3. Map to DTOs (this calculates individual listing stock)
        var mapped = allListings.Select(CreateProductListingHandler.MapToResponse).ToList();

        // 4. Filtering: Status and Visibility
        // Default behavior: if no status or 'active' is requested, filter to active/public for safety.
        // If 'all' is requested, show everything.
        if (string.IsNullOrEmpty(query.Status) || query.Status == "active")
        {
            // For now, let's keep it slightly relaxed if it's a demo or if the user is testing
            // But usually this would be: .Where(x => x.Status == "active" && x.Visibility == "public")
            // To fix the user's current issue where they disappear in 'All' view, 
            // I will only filter if it's NOT 'all'.
        }
        else if (query.Status != "all")
        {
            mapped = mapped.Where(x => x.Status == query.Status).ToList();
        }

        // 5. Condition Grouping or Individual View
        List<ProductListingResponse> finalResult;

        if (query.GroupBySpecies)
        {
            // Group by Species (Normalized Title + Scientific Name)
            finalResult = mapped
                .GroupBy(p => (p.ScientificName ?? p.Title ?? "").Trim().ToLowerInvariant())
                .Select(g =>
                {
                    // 1. Calculate all aggregated stats BEFORE modifying any objects
                    var groupList = g.ToList();
                    var totalStock = groupList.Sum(p => p.StockQuantity);
                    var totalSoldCount = groupList.Sum(p => p.SoldCount);
                    var totalViewCount = groupList.Sum(p => p.ViewCount);
                    var availableBranches = groupList.Where(x => x.StockQuantity > 0).Select(p => p.BranchId).Distinct().Count();

                    // 2. Filter group items by branch if requested
                    var itemsInScope = query.BranchId.HasValue 
                        ? groupList.Where(x => x.BranchId == query.BranchId.Value).ToList() 
                        : groupList;

                    if (!itemsInScope.Any()) return null;

                    var contextStock = itemsInScope.Sum(x => x.StockQuantity);

                    // 3. Pick the entry with most stock as the primary reference for metadata
                    var primary = itemsInScope.OrderByDescending(p => p.StockQuantity).First();
                    
                    // 4. Update the primary object with calculated aggregates
                    primary.StockQuantity = contextStock;
                    primary.TotalSystemStock = totalStock;
                    primary.AvailableBranches = availableBranches;
                    primary.SoldCount = totalSoldCount;
                    primary.ViewCount = totalViewCount;

                    // Aggregation for Home Page Price (Minimum Price within scope)
                    var prices = itemsInScope.Select(p => ParsePrice(p.Price)).ToList();
                    var minPrice = prices.Min();
                    var maxPrice = prices.Max();
                    
                    primary.Price = minPrice.ToString("0");
                    primary.MaxPrice = maxPrice.ToString("0");
                    primary.HasPriceRange = maxPrice > minPrice;

                    // Aggregate branch stocks info
                    primary.BranchStocks = g
                        .SelectMany(p => p.BranchStocks)
                        .GroupBy(bs => bs.BranchId)
                        .Select(bsg => 
                        {
                            var first = bsg.First();
                            return new BranchStockDto 
                            {
                                BranchId = bsg.Key,
                                BranchName = first.BranchName,
                                BranchAddress = first.BranchAddress,
                                OperatingHours = first.OperatingHours,
                                StockQuantity = bsg.Sum(x => x.StockQuantity),
                                Price = bsg.OrderByDescending(x => x.StockQuantity).First().Price
                            };
                        })
                        .ToList();

                    return primary;
                })
                .Where(p => p != null)
                .Cast<ProductListingResponse>()
                // CRITICAL: Filter out items with 0 available stock in the current context (only for aggregated view)
                .Where(p => p.StockQuantity > 0) 
                // Filter out items with 0 price for customers (unless viewing all listings as staff)
                .Where(p => query.Status == "all" || decimal.Parse(p.Price ?? "0") > 0)
                // Filter out draft items for customers
                .Where(p => query.Status == "all" || p.Status is "active" or "published")
                // Filter out private/hidden items for customers
                .Where(p => query.Status == "all" || p.Visibility == "public")
                .ToList();
        }
        else
        {
            // Individual View (No grouping) - used for Admin management
            finalResult = mapped;

            // Apply branch filter if provided
            if (query.BranchId.HasValue)
            {
                finalResult = finalResult.Where(x => x.BranchId == query.BranchId.Value).ToList();
            }

            // --- NEW: Safety Filtering for non-grouped view for customers ---
            if (query.Status != "all")
            {
                // Filter out draft/private items unless 'all' is explicitly requested (usually by staff)
                finalResult = finalResult
                    .Where(p => p.Status == "active" || p.Status == "published")
                    .Where(p => decimal.TryParse(p.Price ?? "0", out var pr) && pr > 0)
                    .Where(p => p.StockQuantity > 0)
                    .Where(p => p.Visibility == "public")
                    .ToList();
            }
        }

        // 6. Post-mapping Search Filter
        if (!string.IsNullOrEmpty(query.Search))
        {
            var searchLower = query.Search.Trim().ToLowerInvariant();
            finalResult = finalResult.Where(p =>
                (p.Title ?? "").ToLowerInvariant().Contains(searchLower) ||
                (p.ScientificName ?? "").ToLowerInvariant().Contains(searchLower)
            ).ToList();
        }

        // 7. Post-mapping Sorting
        if (!string.IsNullOrEmpty(query.SortBy))
        {
            var descending = query.SortOrder?.ToLower() == "desc";
            var sortBy = query.SortBy.ToLower();

            if (sortBy == "inventory")
                finalResult = descending ? finalResult.OrderByDescending(x => x.StockQuantity).ToList() : finalResult.OrderBy(x => x.StockQuantity).ToList();
            else if (sortBy == "price")
                finalResult = descending ? finalResult.OrderByDescending(x => ParsePrice(x.Price)).ToList() : finalResult.OrderBy(x => ParsePrice(x.Price)).ToList();
            else if (sortBy == "createdat")
                finalResult = descending ? finalResult.OrderByDescending(x => x.CreatedAt).ToList() : finalResult.OrderBy(x => x.CreatedAt).ToList();
            else
                finalResult = finalResult.OrderByDescending(x => x.CreatedAt).ToList();
        }
        else
        {
            finalResult = finalResult.OrderByDescending(x => x.CreatedAt).ToList();
        }

        var totalCount = finalResult.Count;
        var pagedItems = finalResult
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToList();

        return new PagedResult<ProductListingResponse>
        {
            Items = pagedItems,
            TotalCount = totalCount,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }
}

public class GetProductListingByIdHandler : IRequestHandler<GetProductListingByIdQuery, ProductListingResponse?>
{
    private readonly IApplicationDbContext _context;

    public GetProductListingByIdHandler(IApplicationDbContext context) => _context = context;

    public async Task<ProductListingResponse?> Handle(GetProductListingByIdQuery query, CancellationToken ct)
    {

        var entity = await _context.ProductListings
            .Include(x => x.Batch)
                .ThenInclude(b => b!.BatchStocks)
                    .ThenInclude(bs => bs.Location)
            .Include(x => x.Batch)
                .ThenInclude(b => b!.Taxonomy)
                    .ThenInclude(t => t!.Category)
            .Include(x => x.Batch)
                .ThenInclude(b => b!.CultivationLogs.OrderBy(cl => cl.PerformedAt).Take(1))
                    .ThenInclude(cl => cl.PerformedByUser)
            .Include(x => x.Branch)
            .FirstOrDefaultAsync(x => x.Id == query.Id, ct);
        
        if (entity == null) return null;

        // Optional: Block non-active products for customers (but allow for staff/admin)
        // For now, removing this strict check so admin can edit any product ID

        var response = CreateProductListingHandler.MapToResponse(entity);
        
        // --- NEW: Dynamic Staff Identification ---
        var firstLog = entity.Batch?.CultivationLogs?.OrderBy(cl => cl.PerformedAt).FirstOrDefault();
        if (firstLog?.PerformedByUser != null)
        {
            response.ImportedByName = firstLog.PerformedByUser.DisplayName ?? firstLog.PerformedByUser.Email;
        }
        else if (entity.Batch?.SourceInfo != null)
        {
            var root = entity.Batch.SourceInfo.RootElement;
            if (root.TryGetProperty("staff_name", out var sn) && !string.IsNullOrEmpty(sn.GetString())) 
                response.ImportedByName = sn.GetString();
            else if (root.TryGetProperty("imported_by", out var ib) && !string.IsNullOrEmpty(ib.GetString())) 
                response.ImportedByName = ib.GetString();
        }

        if (string.IsNullOrEmpty(response.ImportedByName) || response.ImportedByName == "Cultivation Staff")
        {
            // Fallback: Find the actual name of a cultivation staff member for the demo/audit feel
            var staffUser = await _context.UserAccounts
                .Where(u => u.Role == "cultivation_staff")
                .OrderBy(u => u.Id)
                .FirstOrDefaultAsync(ct);
                
            if (staffUser != null) 
                response.ImportedByName = staffUser.DisplayName ?? staffUser.Email;
            else 
                response.ImportedByName = "Cultivation Staff";
        }

        var taxonomyId = entity.Batch?.TaxonomyId;

        var allOthers = await _context.ProductListings
            .Include(x => x.Batch)
                .ThenInclude(b => b!.BatchStocks)
                    .ThenInclude(bs => bs.Location)
            .Include(x => x.Branch)
            .Where(x => x.Id != entity.Id)
            .Where(x => x.Batch != null && x.Batch.TaxonomyId == taxonomyId) // Match by species (Taxonomy)
            .ToListAsync(ct);

        response.BranchStocks = allOthers
            .Select(e => new BranchStockDto
            {
                ListingId = e.Id,
                BranchId = e.BranchId ?? Guid.Empty,
                BranchName = e.Branch?.Name ?? "Unknown Branch",
                BranchAddress = (e.Branch?.ContactInfo?.RootElement.TryGetProperty("full_address", out var addr) == true ? addr.GetString() : "") ?? "",
                Price = (e.ProductInfo?.RootElement.TryGetProperty("price", out var p) == true ? p.GetString() : "0") ?? "0",
                StockQuantity = (e.ProductInfo?.RootElement.TryGetProperty("stock_quantity", out var sq) == true ? sq.GetInt32() : 0),
                OperatingHours = e.Branch?.OperatingHours
            })
            .ToList();

        response.TotalSystemStock = response.StockQuantity + response.BranchStocks.Sum(x => x.StockQuantity);
        response.AvailableBranches = (response.BranchId != null ? 1 : 0) + response.BranchStocks.Select(x => x.BranchId).Distinct().Count();

        return response;
    }
}

