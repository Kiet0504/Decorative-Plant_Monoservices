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
                max_order = req.MaxOrder
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
        }

        // Override with real stock from linked batch if available
        if (e.Batch != null) 
        {
            response.StockQuantity = e.Batch.CurrentTotalQuantity ?? 0;
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
        _ => element.GetRawText()
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

    public async Task<PagedResult<ProductListingResponse>> Handle(GetProductListingsQuery query, CancellationToken ct)
    {
        var q = _context.ProductListings
            .Include(x => x.Batch)
            .AsQueryable();

        if (query.BranchId.HasValue)
        {
            // Admin/branch-specific view: return raw listings without grouping
            q = q.Where(x => x.BranchId == query.BranchId.Value);
            var total = await q.CountAsync(ct);
            var entities = await q.OrderByDescending(x => x.CreatedAt)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync(ct);

            return new PagedResult<ProductListingResponse>
            {
                Items = entities.Select(e =>
                {
                    var r = CreateProductListingHandler.MapToResponse(e);
                    r.TotalSystemStock = r.StockQuantity;
                    r.AvailableBranches = 1;
                    return r;
                }).ToList(),
                TotalCount = total,
                Page = query.Page,
                PageSize = query.PageSize
            };
        }

        // ── Chain Store model: Group listings by title, aggregate stock ──
        var allListings = await q.OrderByDescending(x => x.CreatedAt).ToListAsync(ct);
        var mapped = allListings.Select(CreateProductListingHandler.MapToResponse).ToList();

        // Group by normalized title (case-insensitive, trimmed)
        var grouped = mapped
            .GroupBy(p => (p.Title ?? "").Trim().ToLowerInvariant())
            .Select(g =>
            {
                // Pick the listing with the highest stock as the "primary" representative
                var primary = g.OrderByDescending(p => p.StockQuantity).First();
                primary.TotalSystemStock = g.Sum(p => p.StockQuantity);
                primary.AvailableBranches = g.Select(p => p.BranchId).Distinct().Count();
                // Aggregate sold count & view count
                primary.SoldCount = g.Sum(p => p.SoldCount);
                primary.ViewCount = g.Sum(p => p.ViewCount);
                return primary;
            })
            .ToList();

        // Apply search filter (post-grouping, on title)
        if (!string.IsNullOrEmpty(query.Search))
        {
            var searchLower = query.Search.Trim().ToLowerInvariant();
            grouped = grouped.Where(p =>
                (p.Title ?? "").ToLowerInvariant().Contains(searchLower) ||
                (p.ScientificName ?? "").ToLowerInvariant().Contains(searchLower)
            ).ToList();
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
            .FirstOrDefaultAsync(x => x.Id == query.Id, ct);
        return entity == null ? null : CreateProductListingHandler.MapToResponse(entity);
    }
}
