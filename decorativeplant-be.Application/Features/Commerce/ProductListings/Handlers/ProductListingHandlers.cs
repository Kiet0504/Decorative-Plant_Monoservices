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

    internal static ProductListingResponse MapToResponse(ProductListing e)
    {
        var response = new ProductListingResponse
        {
            Id = e.Id,
            BranchId = e.BranchId,
            BatchId = e.BatchId,
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

        // --- NEW: Force Taxonomy name for consistency in Admin view if Batch data is available ---
        if (e.Batch?.Taxonomy != null)
        {
            if (e.Batch.Taxonomy.CommonNames != null && 
                e.Batch.Taxonomy.CommonNames.RootElement.TryGetProperty("en", out var enProp))
            {
                var taxEn = enProp.GetString();
                if (!string.IsNullOrEmpty(taxEn)) response.Title = taxEn;
            }
            response.ScientificName = e.Batch.Taxonomy.ScientificName;
        }

        // Strictly use real stock from linked batch inventory (BatchStock)
        response.StockQuantity = 0;
        if (e.Batch != null && e.Batch.BatchStocks != null)
        {
            // We use the available quantity at the specific branch location
            var batchStock = e.Batch.BatchStocks.FirstOrDefault(s => s.Location?.BranchId == e.BranchId);
            if (batchStock != null && batchStock.Quantities != null)
            {
                if (batchStock.Quantities.RootElement.TryGetProperty("available_quantity", out var aq))
                {
                    response.StockQuantity = aq.GetInt32();
                }
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
            response.BranchStocks.Add(new BranchStockDto
            {
                ListingId = e.Id,
                BranchId = e.Branch.Id,
                BranchName = e.Branch.Name,
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
            .FirstOrDefaultAsync(x => x.Id == cmd.Id, ct)
            ?? throw new NotFoundException($"ProductListing {cmd.Id} not found.");

        if (req.StockQuantity.HasValue)
        {
            if (entity.Batch != null)
            {
                entity.Batch.CurrentTotalQuantity = req.StockQuantity.Value;
                _logger.LogInformation("Updated existing Batch {BatchId} to quantity {Quantity} for Product {ProductId}", 
                    entity.BatchId, req.StockQuantity.Value, entity.Id);
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
        if (req.Price != null) productInfo["price"] = req.Price;
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

        // 2. Fetch all relevant entries for aggregation
        // Note: We don't filter by BranchId in SQL yet if we want to show 'Global' info 
        // BUT if the query.BranchId is set, the customer only wants to see what's available AT that branch.
        var allListings = await q.ToListAsync(ct);
        
        // 3. Map to DTOs (this calculates individual listing stock)
        var mapped = allListings.Select(CreateProductListingHandler.MapToResponse).ToList();

        // 4. Filtering: Status and Visibility (Customer view should only see public/active)
        // If query status is provided, use it (usually for Admin), otherwise default to active/public
        if (!query.BranchId.HasValue) // Global/Customer view
        {
            mapped = mapped.Where(x => x.Status == "active" && x.Visibility == "public").ToList();
        }
        else if (!string.IsNullOrEmpty(query.Status) && query.Status != "all")
        {
            mapped = mapped.Where(x => x.Status == query.Status).ToList();
        }

        // 5. Group by Species (Normalized Title + Scientific Name)
        // This combines duplicates across batches and branches
        var grouped = mapped
            .GroupBy(p => (p.ScientificName ?? p.Title ?? "").Trim().ToLowerInvariant())
            .Select(g =>
            {
                var primary = g.OrderByDescending(p => p.StockQuantity).First();
                
                // If a branch filter is applied, we only care about stock at THAT branch
                if (query.BranchId.HasValue)
                {
                    primary.StockQuantity = g.Where(x => x.BranchId == query.BranchId.Value).Sum(x => x.StockQuantity);
                }
                else
                {
                    primary.StockQuantity = g.Sum(p => p.StockQuantity);
                }

                primary.TotalSystemStock = g.Sum(p => p.StockQuantity);
                primary.AvailableBranches = g.Where(x => x.StockQuantity > 0).Select(p => p.BranchId).Distinct().Count();
                primary.SoldCount = g.Sum(p => p.SoldCount);
                primary.ViewCount = g.Sum(p => p.ViewCount);

                // Aggregate branch stocks info
                primary.BranchStocks = g
                    .SelectMany(p => p.BranchStocks)
                    .GroupBy(bs => bs.BranchId)
                    .Select(bsg => new BranchStockDto 
                    {
                        BranchId = bsg.Key,
                        BranchName = bsg.First().BranchName,
                        StockQuantity = bsg.Sum(x => x.StockQuantity),
                        Price = bsg.OrderByDescending(x => x.StockQuantity).First().Price
                    })
                    .ToList();

                return primary;
            })
            // CRITICAL: Filter out items with 0 available stock in the current context
            .Where(p => p.StockQuantity > 0) 
            .ToList();

        // 6. Post-grouping Search Filter
        if (!string.IsNullOrEmpty(query.Search))
        {
            var searchLower = query.Search.Trim().ToLowerInvariant();
            grouped = grouped.Where(p =>
                (p.Title ?? "").ToLowerInvariant().Contains(searchLower) ||
                (p.ScientificName ?? "").ToLowerInvariant().Contains(searchLower)
            ).ToList();
        }

        // 7. Post-grouping Sorting
        if (!string.IsNullOrEmpty(query.SortBy))
        {
            var descending = query.SortOrder?.ToLower() == "desc";
            var sortBy = query.SortBy.ToLower();

            if (sortBy == "inventory")
                grouped = descending ? grouped.OrderByDescending(x => x.StockQuantity).ToList() : grouped.OrderBy(x => x.StockQuantity).ToList();
            else if (sortBy == "price")
                grouped = descending ? grouped.OrderByDescending(x => ParsePrice(x.Price)).ToList() : grouped.OrderBy(x => ParsePrice(x.Price)).ToList();
            else if (sortBy == "createdat")
                grouped = descending ? grouped.OrderByDescending(x => x.CreatedAt).ToList() : grouped.OrderBy(x => x.CreatedAt).ToList();
            else
                grouped = grouped.OrderByDescending(x => x.CreatedAt).ToList();
        }
        else
        {
            grouped = grouped.OrderByDescending(x => x.CreatedAt).ToList();
        }

        var totalCount = grouped.Count;
        var pagedItems = grouped
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
            .Include(x => x.Branch)
            .FirstOrDefaultAsync(x => x.Id == query.Id, ct);
        
        if (entity == null) return null;

        var response = CreateProductListingHandler.MapToResponse(entity);
        var normalizedTitle = (response.Title ?? "").Trim().ToLowerInvariant();

        var allOthers = await _context.ProductListings
            .Include(x => x.Batch)
                .ThenInclude(b => b!.BatchStocks)
                    .ThenInclude(bs => bs.Location)
            .Include(x => x.Branch)
            .Where(x => x.Id != entity.Id)
            .ToListAsync(ct);

        var matchingOthers = allOthers
            .Where(e => (GetProductTitle(e) ?? "").Trim().ToLowerInvariant() == normalizedTitle)
            .Select(CreateProductListingHandler.MapToResponse)
            .ToList();

        response.BranchStocks.AddRange(matchingOthers.SelectMany(x => x.BranchStocks));
        response.TotalSystemStock = response.BranchStocks.Sum(x => x.StockQuantity);
        response.AvailableBranches = response.BranchStocks.Select(x => x.BranchId).Distinct().Count();

        return response;
    }

    private static string? GetProductTitle(ProductListing e)
    {
        if (e.ProductInfo == null) return null;
        return e.ProductInfo.RootElement.TryGetProperty("title", out var t) ? t.GetString() : null;
    }
}

