using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using decorativeplant_be.Application.Common.DTOs.Commerce;
using decorativeplant_be.Application.Common.DTOs.Common;
using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Commerce.ProductReviews.Commands;
using decorativeplant_be.Application.Features.Commerce.ProductReviews.Queries;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Application.Features.Commerce.ProductReviews.Handlers;

public class CreateProductReviewHandler : IRequestHandler<CreateProductReviewCommand, ProductReviewResponse>
{
    private readonly IApplicationDbContext _context;
    public CreateProductReviewHandler(IApplicationDbContext context) => _context = context;

    public async Task<ProductReviewResponse> Handle(CreateProductReviewCommand cmd, CancellationToken ct)
    {
        var req = cmd.Request;

        if (req.OrderId.HasValue)
        {
            var existingOrderReviews = await _context.ProductReviews
                .Where(r => r.OrderId == req.OrderId.Value && r.UserId == cmd.UserId)
                .ToListAsync(ct);
            var hasExistingOrderReview = existingOrderReviews.Any(r => !IsDeleted(r));
            if (hasExistingOrderReview)
                throw new BadRequestException("Only one review is allowed per order.");

            var order = await _context.OrderHeaders.Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.Id == req.OrderId.Value, ct)
                ?? throw new BadRequestException("Invalid OrderId for review verification.");

            if (order.UserId != cmd.UserId) throw new BadRequestException("Order does not belong to the authenticated user.");
            if (order.Status != "completed" && order.Status != "delivered") throw new BadRequestException("Order must be completed or delivered to leave a verified review.");
            
            bool hasItem = order.OrderItems != null && order.OrderItems.Any(i => i.ListingId == req.ListingId);
            if (!hasItem) throw new BadRequestException("Order does not contain the reviewed product.");
        }

        var entity = new ProductReview
        {
            Id = Guid.NewGuid(), ListingId = req.ListingId, UserId = cmd.UserId, OrderId = req.OrderId,
            Content = JsonDocument.Parse(JsonSerializer.Serialize(new
            {
                rating = req.Rating, title = req.Title, comment = req.Comment,
                is_verified = req.OrderId.HasValue, helpful_count = 0
            })),
            StatusInfo = JsonDocument.Parse(JsonSerializer.Serialize(new { status = "pending" })),
            Images = req.Images?.Count > 0 ? JsonDocument.Parse(JsonSerializer.Serialize(
                req.Images.Select(i => new { url = i.Url, alt = i.Alt, sort = i.Sort }))) : null,
            CreatedAt = DateTime.UtcNow
        };
        _context.ProductReviews.Add(entity);
        await _context.SaveChangesAsync(ct);
        return MapToResponse(entity);
    }

    internal static ProductReviewResponse MapToResponse(ProductReview e)
    {
        var r = new ProductReviewResponse { Id = e.Id, ListingId = e.ListingId, UserId = e.UserId, OrderId = e.OrderId, CreatedAt = e.CreatedAt };
        if (e.Content != null)
        {
            var root = e.Content.RootElement;
            r.Rating = root.TryGetProperty("rating", out var rt) ? rt.GetInt32() : 0;
            r.Title = root.TryGetProperty("title", out var t) ? t.GetString() : null;
            r.Comment = root.TryGetProperty("comment", out var c) ? c.GetString() : null;
            r.IsVerified = root.TryGetProperty("is_verified", out var iv) && iv.GetBoolean();
            r.HelpfulCount = root.TryGetProperty("helpful_count", out var hc) ? hc.GetInt32() : 0;
        }
        if (e.StatusInfo != null)
            r.Status = e.StatusInfo.RootElement.TryGetProperty("status", out var s) ? s.GetString() ?? "pending" : "pending";
        if (e.Images?.RootElement.ValueKind == JsonValueKind.Array)
            r.Images = e.Images.RootElement.EnumerateArray().Select(img => new ReviewImageDto
            {
                Url = img.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "",
                Alt = img.TryGetProperty("alt", out var a) ? a.GetString() : null,
                Sort = img.TryGetProperty("sort", out var so) ? so.GetInt32() : 0
            }).ToList();
        return r;
    }

    internal static bool IsDeleted(ProductReview review)
    {
        if (review.StatusInfo == null) return false;
        return review.StatusInfo.RootElement.TryGetProperty("status", out var s) && s.GetString() == "deleted";
    }
}

public class UpdateReviewStatusHandler : IRequestHandler<UpdateReviewStatusCommand, ProductReviewResponse>
{
    private readonly IApplicationDbContext _context;
    public UpdateReviewStatusHandler(IApplicationDbContext context) => _context = context;
    public async Task<ProductReviewResponse> Handle(UpdateReviewStatusCommand cmd, CancellationToken ct)
    {
        var entity = await _context.ProductReviews.FindAsync(new object[] { cmd.Id }, ct) ?? throw new NotFoundException($"Review {cmd.Id} not found.");
        entity.StatusInfo = JsonDocument.Parse(JsonSerializer.Serialize(new { status = cmd.Request.Status }));
        await _context.SaveChangesAsync(ct);
        return CreateProductReviewHandler.MapToResponse(entity);
    }
}

public class DeleteProductReviewHandler : IRequestHandler<DeleteProductReviewCommand, bool>
{
    private readonly IApplicationDbContext _context;
    public DeleteProductReviewHandler(IApplicationDbContext context) => _context = context;
    public async Task<bool> Handle(DeleteProductReviewCommand cmd, CancellationToken ct)
    {
        var e = await _context.ProductReviews.FindAsync(new object[] { cmd.Id }, ct) ?? throw new NotFoundException($"Review {cmd.Id} not found.");
        e.StatusInfo = JsonDocument.Parse(JsonSerializer.Serialize(new { status = "deleted" }));
        await _context.SaveChangesAsync(ct); 
        return true;
    }
}

public class GetReviewsByListingHandler : IRequestHandler<GetReviewsByListingQuery, PagedResult<ProductReviewResponse>>
{
    private readonly IApplicationDbContext _context;
    public GetReviewsByListingHandler(IApplicationDbContext context) => _context = context;
    public async Task<PagedResult<ProductReviewResponse>> Handle(GetReviewsByListingQuery q, CancellationToken ct)
    {
        var query = _context.ProductReviews.Where(r => r.ListingId == q.ListingId);
        
        var listRaw = await query.OrderByDescending(r => r.CreatedAt).ToListAsync(ct);
        var excludedDeletes = listRaw.Where(r => !CreateProductReviewHandler.IsDeleted(r)).ToList();
        
        var total = excludedDeletes.Count;
        
        var list = excludedDeletes
            .Skip((q.Page - 1) * q.PageSize)
            .Take(q.PageSize)
            .ToList();
            
        return new PagedResult<ProductReviewResponse>
        {
            Items = list.Select(CreateProductReviewHandler.MapToResponse).ToList(),
            TotalCount = total,
            Page = q.Page,
            PageSize = q.PageSize
        };
    }
}

public class GetAllReviewsHandler : IRequestHandler<GetAllReviewsQuery, PagedResult<ProductReviewResponse>>
{
    private readonly IApplicationDbContext _context;
    public GetAllReviewsHandler(IApplicationDbContext context) => _context = context;

    public async Task<PagedResult<ProductReviewResponse>> Handle(GetAllReviewsQuery q, CancellationToken ct)
    {
        var page = q.Page <= 0 ? 1 : q.Page;
        var pageSize = q.PageSize <= 0 ? 20 : q.PageSize;

        var listRaw = await _context.ProductReviews
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

        var filtered = listRaw.Where(r => !CreateProductReviewHandler.IsDeleted(r)).ToList();
        var total = filtered.Count;
        var pageItems = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        var userIds = pageItems.Where(r => r.UserId.HasValue).Select(r => r.UserId!.Value).Distinct().ToList();
        var listingIds = pageItems.Where(r => r.ListingId.HasValue).Select(r => r.ListingId!.Value).Distinct().ToList();

        var userMap = await _context.UserAccounts
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName ?? u.Email, ct);

        var listings = await _context.ProductListings
            .Where(p => listingIds.Contains(p.Id))
            .ToListAsync(ct);
        var listingMap = listings.ToDictionary(p => p.Id, p =>
        {
            if (p.ProductInfo != null && p.ProductInfo.RootElement.TryGetProperty("title", out var titleProp))
                return titleProp.GetString() ?? "Unknown product";
            return "Unknown product";
        });

        var items = pageItems.Select(r =>
        {
            var dto = CreateProductReviewHandler.MapToResponse(r);
            if (r.UserId.HasValue && userMap.TryGetValue(r.UserId.Value, out var userName)) dto.UserName = userName;
            if (r.ListingId.HasValue && listingMap.TryGetValue(r.ListingId.Value, out var productName)) dto.ProductName = productName;
            return dto;
        }).ToList();

        return new PagedResult<ProductReviewResponse>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }
}

public class GetReviewByIdHandler : IRequestHandler<GetReviewByIdQuery, ProductReviewResponse?>
{
    private readonly IApplicationDbContext _context;
    public GetReviewByIdHandler(IApplicationDbContext context) => _context = context;
    public async Task<ProductReviewResponse?> Handle(GetReviewByIdQuery q, CancellationToken ct)
    {
        var e = await _context.ProductReviews.FindAsync(new object[] { q.Id }, ct);
        return e == null ? null : CreateProductReviewHandler.MapToResponse(e);
    }
}
