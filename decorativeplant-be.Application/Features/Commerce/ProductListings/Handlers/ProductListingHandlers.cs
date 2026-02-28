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
            ProductInfo = JsonDocument.Parse(JsonSerializer.Serialize(new
            {
                title = req.Title,
                slug,
                description = req.Description,
                price = req.Price,
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
                req.Images.Select(i => new { url = i.Url, alt = i.Alt, is_primary = i.IsPrimary, sort_order = i.SortOrder }))) : null,
            CreatedAt = DateTime.UtcNow
        };

        _context.ProductListings.Add(entity);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Created ProductListing {Id}", entity.Id);
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
            response.Slug = root.TryGetProperty("slug", out var s) ? s.GetString() : null;
            response.Description = root.TryGetProperty("description", out var d) ? d.GetString() : null;
            response.Price = root.TryGetProperty("price", out var p) ? p.GetString() ?? "0" : "0";
            response.MinOrder = root.TryGetProperty("min_order", out var mn) ? mn.GetInt32() : 1;
            response.MaxOrder = root.TryGetProperty("max_order", out var mx) ? mx.GetInt32() : 10;
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

    public UpdateProductListingHandler(IApplicationDbContext context) => _context = context;

    public async Task<ProductListingResponse> Handle(UpdateProductListingCommand cmd, CancellationToken ct)
    {
        var entity = await _context.ProductListings.FindAsync(new object[] { cmd.Id }, ct)
            ?? throw new NotFoundException($"ProductListing {cmd.Id} not found.");

        var req = cmd.Request;

        // Merge product_info
        var productInfo = new Dictionary<string, object?>();
        if (entity.ProductInfo != null)
            foreach (var prop in entity.ProductInfo.RootElement.EnumerateObject())
                productInfo[prop.Name] = GetJsonValue(prop.Value);

        if (req.Title != null) productInfo["title"] = req.Title;
        if (req.Slug != null) productInfo["slug"] = req.Slug;
        if (req.Description != null) productInfo["description"] = req.Description;
        if (req.Price != null) productInfo["price"] = req.Price;
        if (req.MinOrder.HasValue) productInfo["min_order"] = req.MinOrder.Value;
        if (req.MaxOrder.HasValue) productInfo["max_order"] = req.MaxOrder.Value;
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
        var q = _context.ProductListings.AsQueryable();
        if (query.BranchId.HasValue)
            q = q.Where(x => x.BranchId == query.BranchId.Value);
            
        // Assuming search by title if requested
        if (!string.IsNullOrEmpty(query.Search))
        {
            // The title is inside the ProductInfo JSON string. EF Core raw translation of JSON properties is complex.
            // For a basic implementation, we skip DB-level search for JSON or use EF Core JSON methods if configured.
            // In a real scenario, full-text search over JSON is preferred. 
        }

        var total = await q.CountAsync(ct);

        var entities = await q.OrderByDescending(x => x.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);
            
        return new PagedResult<ProductListingResponse>
        {
            Items = entities.Select(CreateProductListingHandler.MapToResponse).ToList(),
            TotalCount = total,
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
        var entity = await _context.ProductListings.FindAsync(new object[] { query.Id }, ct);
        return entity == null ? null : CreateProductListingHandler.MapToResponse(entity);
    }
}
